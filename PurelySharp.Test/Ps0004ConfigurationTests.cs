using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using PurelySharp.Analyzer;

namespace PurelySharp.Test
{
    [TestFixture]
    public class Ps0004ConfigurationTests
    {
        [Test]
        public async Task Ps0004_ScopeOff_SuppressesMissingPuritySuggestions()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class TestClass
{
    public int Pure() => 1;
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_scope", "off"));

            Assert.That(DiagnosticMessages(diagnostics), Has.None.Contains("Pure"));
        }

        [Test]
        public async Task Ps0004_LegacyBooleanFalse_SuppressesMissingPuritySuggestions()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class TestClass
{
    public int Pure() => 1;
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure", "false"));

            Assert.That(DiagnosticMessages(diagnostics), Has.None.Contains("Pure"));
        }

        [Test]
        public async Task Ps0004_ScopePublic_ReportsPublicMethodsOnly()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class TestClass
{
    public int PublicPure() => 1;
    internal int InternalPure() => 2;
    private int PrivatePure() => 3;
    protected int ProtectedPure() => 4;
    protected internal int ProtectedInternalPure() => 5;
    private protected int PrivateProtectedPure() => 6;
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_scope", "public"));

            var messages = DiagnosticMessages(diagnostics);
            Assert.That(messages, Has.Some.Contains("PublicPure"));
            Assert.That(messages, Has.Some.Contains("ProtectedPure"));
            Assert.That(messages, Has.Some.Contains("ProtectedInternalPure"));
            Assert.That(messages, Has.None.Contains("Method 'InternalPure'"));
            Assert.That(messages, Has.None.Contains("Method 'PrivatePure'"));
            Assert.That(messages, Has.None.Contains("PrivateProtectedPure"));
        }

        [Test]
        public async Task Ps0004_ScopeInternal_ReportsInternalMethodsOnly()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class TestClass
{
    public int PublicPure() => 1;
    internal int InternalPure() => 2;
    private int PrivatePure() => 3;
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_scope", "internal"));

            var messages = DiagnosticMessages(diagnostics);
            Assert.That(messages, Has.None.Contains("PublicPure"));
            Assert.That(messages, Has.Some.Contains("InternalPure"));
            Assert.That(messages, Has.None.Contains("PrivatePure"));
        }

        [Test]
        public async Task Ps0004_ExcludeTests_SuppressesTestNamedCode()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
namespace Acme.Tests
{
    public class CalculatorTests
    {
        public int Pure() => 1;
    }
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_exclude_tests", "true"));

            Assert.That(DiagnosticMessages(diagnostics), Has.None.Contains("Pure"));
        }

        [Test]
        public async Task Ps0004_ExcludeGenerated_SuppressesGeneratedFilePaths()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class GeneratedType
{
    public int Pure() => 1;
}",
                ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_exclude_generated", "true"),
                Path.Combine("obj", "Generated.g.cs"));

            Assert.That(DiagnosticMessages(diagnostics), Has.None.Contains("Pure"));
        }

        [Test]
        public async Task Ps0004_NamespaceFilters_ReportOnlyMatchingNamespaces()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
namespace Allowed.Feature
{
    public class Calculator
    {
        public int AllowedPure() => 1;
    }
}

namespace Other.Feature
{
    public class Calculator
    {
        public int OtherPure() => 2;
    }
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_namespace_filters", "Allowed"));

            var messages = DiagnosticMessages(diagnostics);
            Assert.That(messages, Has.Some.Contains("AllowedPure"));
            Assert.That(messages, Has.None.Contains("OtherPure"));
        }

        [Test]
        public async Task Ps0004_MinComplexity_SuppressesTinyMethods()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
public class TestClass
{
    public int Tiny() => 1;

    public int Bigger(int x)
    {
        var y = x + 1;
        var z = y * 2;
        return z;
    }
}", ImmutableDictionary<string, string>.Empty.Add("purelysharp_suggest_missing_enforce_pure_min_complexity", "3"));

            var messages = DiagnosticMessages(diagnostics);
            Assert.That(messages, Has.None.Contains("Tiny"));
            Assert.That(messages, Has.Some.Contains("Bigger"));
        }

        private static ImmutableArray<string> DiagnosticMessages(ImmutableArray<Diagnostic> diagnostics)
        {
            return diagnostics
                .Where(diagnostic => diagnostic.Id == PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                .Select(diagnostic => diagnostic.GetMessage())
                .ToImmutableArray();
        }

        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
            string source,
            ImmutableDictionary<string, string> globalOptions,
            string? filePath = null)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Preview),
                path: filePath ?? Path.Combine("src", "ProductionCode.cs"));
            var references = GetTrustedPlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(PurelySharp.Attributes.EnforcePureAttribute).Assembly.Location));

            var compilation = CSharpCompilation.Create(
                "Ps0004ConfigurationTests",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzerOptions = new AnalyzerOptions(
                ImmutableArray<AdditionalText>.Empty,
                new TestAnalyzerConfigOptionsProvider(globalOptions));

            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new PurelySharpAnalyzer()),
                new CompilationWithAnalyzersOptions(
                    analyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
        {
            var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            {
                return ImmutableArray.Create<MetadataReference>(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
            }

            return trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToImmutableArray();
        }

        private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _globalOptions;
            private readonly AnalyzerConfigOptions _emptyOptions = new TestAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

            public TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
            {
                _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
            }

            public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _emptyOptions;
        }

        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly ImmutableDictionary<string, string> _values;

            public TestAnalyzerConfigOptions(ImmutableDictionary<string, string> values)
            {
                _values = values;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (_values.TryGetValue(key, out var found))
                {
                    value = found;
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
