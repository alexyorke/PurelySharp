using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;

namespace PurelySharp.Tools.Fuzz;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
        {
            Console.WriteLine(FuzzOptions.Usage);
            return 0;
        }

        try
        {
            var options = FuzzOptions.Parse(args);
            var summary = await FuzzRunner.RunAsync(options);

            if (!options.Quiet)
            {
                Console.WriteLine($"PurelySharp fuzz run complete: {summary.CasesAnalyzed} cases, {summary.FindingCount} findings, {summary.AnalyzerExceptionCount} analyzer exceptions.");
                Console.WriteLine($"Artifacts: {summary.OutputDirectory}");
            }

            return options.FailOnFindings && summary.FindingCount > 0 ? 2 : 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(FuzzOptions.Usage);
            return 64;
        }
    }
}

public sealed record FuzzOptions
{
    public const string Usage = """
Usage: PurelySharp.Fuzz [options]

Options:
  --iterations <n>         Number of generated cases. Use 0 for duration-only runs. Default: 100.
  --seconds <n>            Run duration in seconds.
  --minutes <n>            Run duration in minutes.
  --hours <n>              Run duration in hours.
  --seed <n>               Deterministic random seed. Default: 12345.
  --out <path>             Output directory. Default: artifacts/fuzz/<timestamp>.
  --max-interesting <n>    Maximum source files saved for findings. Default: 100.
  --quiet                  Suppress progress output.
  --fail-on-findings       Exit with code 2 when findings are found.
  --no-repeat              Do not run repeated analyzer determinism checks.
""";

    public int? Iterations { get; init; } = 100;

    public TimeSpan? Duration { get; init; }

    public int Seed { get; init; } = 12345;

    public string OutputDirectory { get; init; } = DefaultOutputDirectory();

    public int MaxInterestingCases { get; init; } = 100;

    public bool Quiet { get; init; }

    public bool FailOnFindings { get; init; }

    public bool RepeatAnalyzer { get; init; } = true;

    public static FuzzOptions Parse(string[] args)
    {
        var options = new FuzzOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--iterations":
                    options = options with { Iterations = ReadInt(args, ref i, arg) };
                    break;
                case "--seconds":
                    options = options with { Duration = TimeSpan.FromSeconds(ReadDouble(args, ref i, arg)) };
                    break;
                case "--minutes":
                    options = options with { Duration = TimeSpan.FromMinutes(ReadDouble(args, ref i, arg)) };
                    break;
                case "--hours":
                    options = options with { Duration = TimeSpan.FromHours(ReadDouble(args, ref i, arg)) };
                    break;
                case "--seed":
                    options = options with { Seed = ReadInt(args, ref i, arg) };
                    break;
                case "--out":
                    options = options with { OutputDirectory = ReadString(args, ref i, arg) };
                    break;
                case "--max-interesting":
                    options = options with { MaxInterestingCases = ReadInt(args, ref i, arg) };
                    break;
                case "--quiet":
                    options = options with { Quiet = true };
                    break;
                case "--fail-on-findings":
                    options = options with { FailOnFindings = true };
                    break;
                case "--no-repeat":
                    options = options with { RepeatAnalyzer = false };
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arg}'.");
            }
        }

        if (options.Iterations < 0)
        {
            throw new ArgumentException("--iterations must be non-negative.");
        }

        if (options.MaxInterestingCases < 0)
        {
            throw new ArgumentException("--max-interesting must be non-negative.");
        }

        if (options.Iterations == 0 && options.Duration is null)
        {
            throw new ArgumentException("Duration-only runs need --seconds, --minutes, or --hours when --iterations is 0.");
        }

        return options;
    }

    private static int ReadInt(string[] args, ref int index, string option)
    {
        var value = ReadString(args, ref index, option);
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new ArgumentException($"{option} expects an integer.");
    }

    private static double ReadDouble(string[] args, ref int index, string option)
    {
        var value = ReadString(args, ref index, option);
        return double.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : throw new ArgumentException($"{option} expects a non-negative number.");
    }

    private static string ReadString(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} expects a value.");
        }

        index++;
        return args[index];
    }

    private static string DefaultOutputDirectory()
    {
        return Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "fuzz",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
    }
}

public static class FuzzRunner
{
    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<FuzzRunSummary> RunAsync(FuzzOptions options, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var interestingDirectory = Path.Combine(options.OutputDirectory, "interesting-cases");
        Directory.CreateDirectory(interestingDirectory);

        var startedUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var generator = new FuzzCaseGenerator(options.Seed);
        var builder = new FuzzRunSummaryBuilder(options, startedUtc);
        var maxIterations = options.Iterations is > 0 ? options.Iterations.Value : (int?)null;
        var deadline = options.Duration is { } duration ? startedUtc + duration : (DateTimeOffset?)null;
        var savedInterestingCases = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (maxIterations is { } max && builder.CasesAnalyzed >= max)
            {
                break;
            }

            if (deadline is { } end && DateTimeOffset.UtcNow >= end)
            {
                break;
            }

            var fuzzCase = generator.Next(builder.CasesAnalyzed);
            var analysis = await AnalyzeCaseAsync(fuzzCase, options.RepeatAnalyzer, cancellationToken);

            if (analysis.Findings.Length > 0 && savedInterestingCases < options.MaxInterestingCases)
            {
                var fileName = $"{savedInterestingCases + 1:0000}-{SanitizeFileName(fuzzCase.Name)}.cs";
                var sourcePath = Path.Combine(interestingDirectory, fileName);
                await File.WriteAllTextAsync(sourcePath, fuzzCase.Source, cancellationToken);
                analysis = analysis with
                {
                    Findings = analysis.Findings
                        .Select(finding => finding with { SourcePath = sourcePath })
                        .ToImmutableArray()
                };
                savedInterestingCases++;
            }

            builder.Add(analysis);
        }

        stopwatch.Stop();
        var summary = builder.Build(DateTimeOffset.UtcNow, stopwatch.Elapsed, options.OutputDirectory);
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, "summary.json"), summaryJson, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory, "coverage.json"), CreateCoverageJson(summary), cancellationToken);

        return summary;
    }

    public static async Task<FuzzCaseAnalysis> AnalyzeCaseAsync(
        FuzzCase fuzzCase,
        bool repeatAnalyzer = true,
        CancellationToken cancellationToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(fuzzCase.Source, ParseOptions, cancellationToken: cancellationToken);
        var compilation = CreateCompilation(fuzzCase.Name, syntaxTree, fuzzCase.AllowUnsafe);
        var compilerErrors = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToImmutableArray();

        if (compilerErrors.Length > 0)
        {
            return new FuzzCaseAnalysis(
                fuzzCase,
                ImmutableSortedDictionary<string, int>.Empty,
                CollectSyntaxKinds(syntaxTree),
                ImmutableArray<Diagnostic>.Empty,
                ImmutableArray<string>.Empty,
                compilerErrors,
                ImmutableArray.Create(new FuzzFinding(
                    fuzzCase.Name,
                    fuzzCase.Family,
                    "compilation_error",
                    "Generated source did not compile.",
                    null,
                    compilerErrors)));
        }

        var operationKinds = CollectOperationKinds(compilation, syntaxTree, cancellationToken);
        var syntaxKinds = CollectSyntaxKinds(syntaxTree);
        var firstDiagnostics = await GetAnalyzerDiagnosticsAsync(compilation, cancellationToken);
        var findings = Evaluate(fuzzCase, firstDiagnostics.Diagnostics, firstDiagnostics.Exceptions);
        var diagnosticSignatures = ToDiagnosticSignatures(firstDiagnostics.Diagnostics);

        if (repeatAnalyzer)
        {
            var secondDiagnostics = await GetAnalyzerDiagnosticsAsync(compilation, cancellationToken);
            var secondDiagnosticSignatures = ToDiagnosticSignatures(secondDiagnostics.Diagnostics);
            if (!diagnosticSignatures.SequenceEqual(secondDiagnosticSignatures, StringComparer.Ordinal))
            {
                findings.Add(new FuzzFinding(
                    fuzzCase.Name,
                    fuzzCase.Family,
                    "nondeterministic_diagnostics",
                    "Repeated analyzer runs produced different diagnostic signatures.",
                    null,
                    diagnosticSignatures.Concat(secondDiagnosticSignatures).ToImmutableArray()));
            }

            foreach (var exception in secondDiagnostics.Exceptions)
            {
                findings.Add(new FuzzFinding(
                    fuzzCase.Name,
                    fuzzCase.Family,
                    "analyzer_exception",
                    exception,
                    null,
                    ImmutableArray<string>.Empty));
            }
        }

        return new FuzzCaseAnalysis(
            fuzzCase,
            operationKinds,
            syntaxKinds,
            firstDiagnostics.Diagnostics,
            diagnosticSignatures,
            ImmutableArray<string>.Empty,
            findings.ToImmutable());
    }

    private static AnalyzerRunResult EmptyAnalyzerRun => new(ImmutableArray<Diagnostic>.Empty, ImmutableArray<string>.Empty);

    private static async Task<AnalyzerRunResult> GetAnalyzerDiagnosticsAsync(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new PurelySharpAnalyzer()),
                new CompilationWithAnalyzersOptions(
                    options,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
            return new AnalyzerRunResult(diagnostics, ImmutableArray<string>.Empty);
        }
        catch (Exception ex)
        {
            return EmptyAnalyzerRun with { Exceptions = ImmutableArray.Create(ex.ToString()) };
        }
    }

    private static ImmutableArray<FuzzFinding>.Builder Evaluate(
        FuzzCase fuzzCase,
        ImmutableArray<Diagnostic> diagnostics,
        ImmutableArray<string> analyzerExceptions)
    {
        var findings = ImmutableArray.CreateBuilder<FuzzFinding>();
        foreach (var exception in analyzerExceptions)
        {
            findings.Add(new FuzzFinding(
                fuzzCase.Name,
                fuzzCase.Family,
                "analyzer_exception",
                exception,
                null,
                ImmutableArray<string>.Empty));
        }

        var ps0002Diagnostics = diagnostics
            .Where(diagnostic => diagnostic.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
            .ToImmutableArray();

        if (fuzzCase.Expectation.Kind == FuzzExpectationKind.DefinitelyPure && ps0002Diagnostics.Length > 0)
        {
            findings.Add(new FuzzFinding(
                fuzzCase.Name,
                fuzzCase.Family,
                "pure_ps0002",
                "A definitely-pure generated case produced PS0002.",
                null,
                ToDiagnosticSignatures(ps0002Diagnostics)));
        }

        if (fuzzCase.Expectation.Kind == FuzzExpectationKind.DefinitelyImpure && ps0002Diagnostics.Length == 0)
        {
            findings.Add(new FuzzFinding(
                fuzzCase.Name,
                fuzzCase.Family,
                "impure_missing_ps0002",
                "A definitely-impure generated case did not produce PS0002.",
                null,
                ToDiagnosticSignatures(diagnostics)));
        }

        foreach (var diagnostic in ps0002Diagnostics)
        {
            if (MissingProperty(diagnostic, PurelySharpDiagnostics.ImpurityCategoryProperty) ||
                MissingProperty(diagnostic, PurelySharpDiagnostics.ImpurityRuleProperty) ||
                MissingProperty(diagnostic, PurelySharpDiagnostics.ImpurityOperationKindProperty))
            {
                findings.Add(new FuzzFinding(
                    fuzzCase.Name,
                    fuzzCase.Family,
                    "missing_ps0002_evidence",
                    "PS0002 did not include stable category/rule/operation evidence.",
                    null,
                    ImmutableArray.Create(ToDiagnosticSignature(diagnostic))));
            }

            if (fuzzCase.Expectation.Kind == FuzzExpectationKind.DefinitelyPure &&
                diagnostic.Properties.TryGetValue(PurelySharpDiagnostics.ImpurityCategoryProperty, out var category) &&
                string.Equals(category, "unsupported_operation", StringComparison.Ordinal))
            {
                findings.Add(new FuzzFinding(
                    fuzzCase.Name,
                    fuzzCase.Family,
                    "pure_unsupported_operation",
                    "A definitely-pure generated case hit unsupported_operation.",
                    null,
                    ImmutableArray.Create(ToDiagnosticSignature(diagnostic))));
            }
        }

        return findings;
    }

    private static bool MissingProperty(Diagnostic diagnostic, string key)
    {
        return !diagnostic.Properties.TryGetValue(key, out var value) ||
               string.IsNullOrWhiteSpace(value);
    }

    private static ImmutableSortedDictionary<string, int> CollectOperationKinds(
        Compilation compilation,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken)
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in syntaxTree.GetRoot(cancellationToken).DescendantNodes())
        {
            var operation = semanticModel.GetOperation(node, cancellationToken);
            if (operation is null)
            {
                continue;
            }

            foreach (var descendant in operation.DescendantsAndSelf())
            {
                Increment(counts, descendant.Kind.ToString());
            }
        }

        return counts.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }

    private static ImmutableSortedDictionary<string, int> CollectSyntaxKinds(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetRoot();
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        Increment(counts, ((SyntaxKind)root.RawKind).ToString());

        foreach (var nodeOrToken in root.DescendantNodesAndTokens(descendIntoTrivia: true))
        {
            Increment(counts, ((SyntaxKind)nodeOrToken.RawKind).ToString());
        }

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            Increment(counts, ((SyntaxKind)trivia.RawKind).ToString());
            var structure = trivia.GetStructure();
            if (structure is null)
            {
                continue;
            }

            Increment(counts, ((SyntaxKind)structure.RawKind).ToString());
            foreach (var nodeOrToken in structure.DescendantNodesAndTokens(descendIntoTrivia: true))
            {
                Increment(counts, ((SyntaxKind)nodeOrToken.RawKind).ToString());
            }
        }

        return counts.ToImmutableSortedDictionary(StringComparer.Ordinal);
    }

    private static CSharpCompilation CreateCompilation(string assemblyName, SyntaxTree syntaxTree, bool allowUnsafe)
    {
        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES was not available.");
        }

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(EnforcePureAttribute).Assembly.Location)
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(group => (MetadataReference)MetadataReference.CreateFromFile(group.Key))
            .ToImmutableArray();
    }

    private static ImmutableArray<string> ToDiagnosticSignatures(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Select(ToDiagnosticSignature)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static string ToDiagnosticSignature(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var character = lineSpan.StartLinePosition.Character + 1;
        var properties = string.Join(
            ";",
            diagnostic.Properties
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value));

        return $"{diagnostic.Id}|{line}:{character}|{diagnostic.GetMessage()}|{properties}";
    }

    private static string CreateCoverageJson(FuzzRunSummary summary)
    {
        var coverage = new
        {
            summary.SchemaVersion,
            summary.Seed,
            summary.CasesAnalyzed,
            summary.OperationKinds,
            summary.SyntaxKinds,
            summary.UnobservedOperationKinds,
            summary.FamilyCounts
        };

        return JsonSerializer.Serialize(coverage, JsonOptions);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToImmutableHashSet();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static void Increment(IDictionary<string, int> values, string key)
    {
        values[key] = values.TryGetValue(key, out var count) ? count + 1 : 1;
    }
}

public sealed class FuzzCaseGenerator
{
    private readonly Random _random;

    private static readonly ImmutableArray<CaseFamily> Families = ImmutableArray.Create(
        new CaseFamily("PureArithmetic", FuzzExpectation.DefinitelyPure(), BuildPureArithmetic),
        new CaseFamily("PureStringConcat", FuzzExpectation.DefinitelyPure(), BuildPureStringConcat),
        new CaseFamily("PureListPattern", FuzzExpectation.DefinitelyPure(), BuildPureListPattern),
        new CaseFamily("PureCollectionExpression", FuzzExpectation.DefinitelyPure(), BuildPureCollectionExpression),
        new CaseFamily("ImpureConsoleWrite", FuzzExpectation.DefinitelyImpure(), BuildImpureConsoleWrite),
        new CaseFamily("ImpureDynamicDispatch", FuzzExpectation.DefinitelyImpure(), BuildImpureDynamicDispatch),
        new CaseFamily("ImpureDelegateInvoke", FuzzExpectation.DefinitelyImpure(), BuildImpureDelegateInvoke),
        new CaseFamily("ImpureThrowExpression", FuzzExpectation.DefinitelyImpure(), BuildImpureThrowExpression),
        new CaseFamily("ImpureFieldWrite", FuzzExpectation.DefinitelyImpure(), BuildImpureFieldWrite),
        new CaseFamily("ImpureAmbientDateTime", FuzzExpectation.DefinitelyImpure(), BuildImpureAmbientDateTime),
        new CaseFamily("ConservativeInterfaceGetter", FuzzExpectation.Conservative(), BuildConservativeInterfaceGetter),
        new CaseFamily("ConservativeFunctionPointer", FuzzExpectation.Conservative(), BuildConservativeFunctionPointer));

    public FuzzCaseGenerator(int seed)
    {
        _random = new Random(seed);
    }

    public FuzzCase Next(int index)
    {
        var family = Families[_random.Next(Families.Length)];
        var className = $"FuzzCase{index}_{family.Name}";
        var source = family.Build(index, _random, className);
        return new FuzzCase(
            Name: $"{index:000000}-{family.Name}",
            Family: family.Name,
            Source: source,
            AllowUnsafe: source.Contains("unsafe", StringComparison.Ordinal) ||
                         source.Contains("delegate*", StringComparison.Ordinal),
            Expectation: family.Expectation);
    }

    private static string BuildPureArithmetic(int index, Random random, string className)
    {
        var expression = random.Next(4) switch
        {
            0 => "x + 1",
            1 => "(x * 3) - 7",
            2 => "Math.Abs(x)",
            _ => "unchecked((x << 1) ^ 17)"
        };

        return BuildClass(
            className,
            BuildIntMethodFromExpression(expression, random));
    }

    private static string BuildPureStringConcat(int index, Random random, string className)
    {
        var expression = random.Next(2) == 0
            ? "string.Concat(left, right).Length"
            : "(left + right).Length";

        return BuildClass(
            className,
            $$"""
                [EnforcePure]
                public int TestMethod(string left, string right)
                {
            {{Indent(BuildReturnBody(expression, random), 8)}}
                }
            """);
    }

    private static string BuildPureListPattern(int index, Random random, string className)
    {
        var expression = random.Next(2) == 0
            ? "values is [1, .., 3] ? 1 : 0"
            : "values is [_, .. var rest] ? rest.Length : 0";

        return BuildClass(
            className,
            BuildIntMethodFromExpression(expression, random, "int[] values"));
    }

    private static string BuildPureCollectionExpression(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                [EnforcePure]
                public int TestMethod(int x)
                {
                    int[] values = [1, x, 3];
                    return values.Length;
                }
            """);
    }

    private static string BuildImpureConsoleWrite(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                [EnforcePure]
                public void TestMethod()
                {
                    Console.WriteLine("impure");
                }
            """);
    }

    private static string BuildImpureDynamicDispatch(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                [EnforcePure]
                public string TestMethod(dynamic value)
                {
                    return value.ToString();
                }
            """);
    }

    private static string BuildImpureDelegateInvoke(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                [EnforcePure]
                public void TestMethod(Action action)
                {
                    action();
                }
            """);
    }

    private static string BuildImpureThrowExpression(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                [EnforcePure]
                public int TestMethod()
                {
                    return throw new InvalidOperationException("fuzz");
                }
            """);
    }

    private static string BuildImpureFieldWrite(int index, Random random, string className)
    {
        return BuildClass(
            className,
            """
                private int _value;

                [EnforcePure]
                public void TestMethod(int value)
                {
                    _value = value;
                }
            """);
    }

    private static string BuildImpureAmbientDateTime(int index, Random random, string className)
    {
        return BuildClass(
            className,
            BuildIntMethodFromExpression("DateTime.Now.Day", random));
    }

    private static string BuildConservativeInterfaceGetter(int index, Random random, string className)
    {
        return $$"""
using PurelySharp.Attributes;

public interface I{{className}}Value
{
    int Value { get; }
}

public class {{className}}
{
    [EnforcePure]
    public int TestMethod(I{{className}}Value value)
    {
        return value.Value;
    }
}
""";
    }

    private static string BuildConservativeFunctionPointer(int index, Random random, string className)
    {
        return $$"""
using PurelySharp.Attributes;

public unsafe class {{className}}
{
    [EnforcePure]
    public int TestMethod(delegate*<int, int> pointer)
    {
        return pointer(1);
    }
}
""";
    }

    private static string BuildIntMethodFromExpression(string expression, Random random, string parameterList = "int x")
    {
        return $$"""
            [EnforcePure]
            public int TestMethod({{parameterList}})
            {
{{Indent(BuildReturnBody(expression, random), 8)}}
            }
""";
    }

    private static string BuildReturnBody(string expression, Random random)
    {
        return random.Next(5) switch
        {
            0 => $"return {expression};",
            1 => $"var value = {expression};\nreturn value;",
            2 => $"if (true)\n{{\n    return {expression};\n}}\nreturn 0;",
            3 => $"return true ? {expression} : 0;",
            _ => $"int Local() => {expression};\nreturn Local();"
        };
    }

    private static string BuildClass(string className, string members)
    {
        return $$"""
using System;
using PurelySharp.Attributes;

public class {{className}}
{
{{Indent(members, 4)}}
}
""";
    }

    private static string Indent(string text, int spaces)
    {
        var padding = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.Length == 0 ? line : padding + line));
    }

    private sealed record CaseFamily(
        string Name,
        FuzzExpectation Expectation,
        Func<int, Random, string, string> Build);
}

public sealed record FuzzCase(
    string Name,
    string Family,
    string Source,
    bool AllowUnsafe,
    FuzzExpectation Expectation);

public sealed record FuzzExpectation(FuzzExpectationKind Kind)
{
    public static FuzzExpectation DefinitelyPure()
    {
        return new FuzzExpectation(FuzzExpectationKind.DefinitelyPure);
    }

    public static FuzzExpectation DefinitelyImpure()
    {
        return new FuzzExpectation(FuzzExpectationKind.DefinitelyImpure);
    }

    public static FuzzExpectation Conservative()
    {
        return new FuzzExpectation(FuzzExpectationKind.Conservative);
    }
}

public enum FuzzExpectationKind
{
    DefinitelyPure,
    DefinitelyImpure,
    Conservative
}

public sealed record FuzzCaseAnalysis(
    FuzzCase Case,
    ImmutableSortedDictionary<string, int> OperationKinds,
    ImmutableSortedDictionary<string, int> SyntaxKinds,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<string> DiagnosticSignatures,
    ImmutableArray<string> CompilationErrors,
    ImmutableArray<FuzzFinding> Findings);

public sealed record FuzzFinding(
    string CaseName,
    string Family,
    string Category,
    string Description,
    string? SourcePath,
    ImmutableArray<string> Details);

public sealed record FuzzRunSummary
{
    public string SchemaVersion { get; init; } = "1.0";

    public int Seed { get; init; }

    public int? IterationsRequested { get; init; }

    public double? DurationSecondsRequested { get; init; }

    public string OutputDirectory { get; init; } = "";

    public DateTimeOffset StartedUtc { get; init; }

    public DateTimeOffset CompletedUtc { get; init; }

    public double ElapsedSeconds { get; init; }

    public int CasesAnalyzed { get; init; }

    public int CompilationErrorCount { get; init; }

    public int AnalyzerExceptionCount { get; init; }

    public int FindingCount { get; init; }

    public int Ps0002Count { get; init; }

    public int Ps0004Count { get; init; }

    public int Ps0009Count { get; init; }

    public int Ps0010Count { get; init; }

    public ImmutableSortedDictionary<string, int> FamilyCounts { get; init; } =
        ImmutableSortedDictionary<string, int>.Empty;

    public ImmutableSortedDictionary<string, int> OperationKinds { get; init; } =
        ImmutableSortedDictionary<string, int>.Empty;

    public ImmutableSortedDictionary<string, int> SyntaxKinds { get; init; } =
        ImmutableSortedDictionary<string, int>.Empty;

    public ImmutableArray<string> UnobservedOperationKinds { get; init; } =
        ImmutableArray<string>.Empty;

    public ImmutableArray<FuzzFinding> Findings { get; init; } =
        ImmutableArray<FuzzFinding>.Empty;
}

internal sealed class FuzzRunSummaryBuilder
{
    private readonly FuzzOptions _options;
    private readonly DateTimeOffset _startedUtc;
    private readonly SortedDictionary<string, int> _familyCounts = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _operationKinds = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _syntaxKinds = new(StringComparer.Ordinal);
    private readonly ImmutableArray<FuzzFinding>.Builder _findings = ImmutableArray.CreateBuilder<FuzzFinding>();

    private int _compilationErrorCount;
    private int _ps0002Count;
    private int _ps0004Count;
    private int _ps0009Count;
    private int _ps0010Count;

    public FuzzRunSummaryBuilder(FuzzOptions options, DateTimeOffset startedUtc)
    {
        _options = options;
        _startedUtc = startedUtc;
    }

    public int CasesAnalyzed { get; private set; }

    public void Add(FuzzCaseAnalysis analysis)
    {
        CasesAnalyzed++;
        Increment(_familyCounts, analysis.Case.Family);
        AddAll(_operationKinds, analysis.OperationKinds);
        AddAll(_syntaxKinds, analysis.SyntaxKinds);
        _compilationErrorCount += analysis.CompilationErrors.Length > 0 ? 1 : 0;
        _findings.AddRange(analysis.Findings);

        foreach (var diagnostic in analysis.Diagnostics)
        {
            if (diagnostic.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
            {
                _ps0002Count++;
            }
            else if (diagnostic.Id == PurelySharpDiagnostics.MissingEnforcePureAttributeId)
            {
                _ps0004Count++;
            }
            else if (diagnostic.Id == PurelySharpDiagnostics.PurityExplanationId)
            {
                _ps0009Count++;
            }
            else if (diagnostic.Id == PurelySharpDiagnostics.ExceptionSummaryId)
            {
                _ps0010Count++;
            }
        }
    }

    public FuzzRunSummary Build(DateTimeOffset completedUtc, TimeSpan elapsed, string outputDirectory)
    {
        var findings = _findings.ToImmutable();
        var analyzerExceptionCount = findings.Count(finding => finding.Category == "analyzer_exception");
        var observedOperationKinds = _operationKinds.Keys.ToImmutableHashSet(StringComparer.Ordinal);
        var unobservedOperationKinds = Enum.GetNames<OperationKind>()
            .Where(kind => !observedOperationKinds.Contains(kind))
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToImmutableArray();

        return new FuzzRunSummary
        {
            Seed = _options.Seed,
            IterationsRequested = _options.Iterations,
            DurationSecondsRequested = _options.Duration?.TotalSeconds,
            OutputDirectory = outputDirectory,
            StartedUtc = _startedUtc,
            CompletedUtc = completedUtc,
            ElapsedSeconds = elapsed.TotalSeconds,
            CasesAnalyzed = CasesAnalyzed,
            CompilationErrorCount = _compilationErrorCount,
            AnalyzerExceptionCount = analyzerExceptionCount,
            FindingCount = findings.Length,
            Ps0002Count = _ps0002Count,
            Ps0004Count = _ps0004Count,
            Ps0009Count = _ps0009Count,
            Ps0010Count = _ps0010Count,
            FamilyCounts = _familyCounts.ToImmutableSortedDictionary(StringComparer.Ordinal),
            OperationKinds = _operationKinds.ToImmutableSortedDictionary(StringComparer.Ordinal),
            SyntaxKinds = _syntaxKinds.ToImmutableSortedDictionary(StringComparer.Ordinal),
            UnobservedOperationKinds = unobservedOperationKinds,
            Findings = findings
        };
    }

    private static void AddAll(SortedDictionary<string, int> target, IReadOnlyDictionary<string, int> source)
    {
        foreach (var pair in source)
        {
            target[pair.Key] = target.TryGetValue(pair.Key, out var count) ? count + pair.Value : pair.Value;
        }
    }

    private static void Increment(SortedDictionary<string, int> values, string key)
    {
        values[key] = values.TryGetValue(key, out var count) ? count + 1 : 1;
    }
}

internal sealed record AnalyzerRunResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<string> Exceptions);
