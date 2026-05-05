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
    public class DiagnosticEvidenceTests
    {
        [Test]
        public async Task Ps0002_KnownImpureCatalogHit_IncludesStructuredEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine(""impure"");
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("Invocation"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("known_impure_namespace_or_type"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_ImpureCallee_IncludesCalleeChain()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void Caller()
    {
        Callee();
    }

    [EnforcePure]
    public void Callee()
    {
        Console.WriteLine(""impure"");
    }
}");

            var callerDiagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'Caller'", StringComparison.Ordinal));

            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.Callee"));
            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_UnresolvedDelegateTarget_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Action action)
    {
        action();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unresolved_delegate_target"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Action.Invoke"));
        }

        [Test]
        public async Task Ps0002_DynamicDispatch_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(dynamic value)
    {
        return value.ToString();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("dynamic_dispatch"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("DynamicInvocation"));
        }

        [Test]
        public async Task Ps0002_SourceExternCall_IncludesUnknownExternalCallCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System.Runtime.InteropServices;
using PurelySharp.Attributes;

public static class NativeMethods
{
    [DllImport(""native.dll"")]
    public static extern int ReadValue();
}

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return NativeMethods.ReadValue();
    }
}");

            var diagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'TestMethod'", StringComparison.Ordinal));

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unknown_external_call"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("NativeMethods.ReadValue"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("extern"));
        }

        [Test]
        public async Task Ps0002_MutableStateWrite_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    private int _value;

    [EnforcePure]
    public void TestMethod()
    {
        _value = 1;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_write"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("AssignmentPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass._value"));
        }

        [Test]
        public async Task Ps0002_AssignmentRhsImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        int value;
        value = Console.Read();
        return value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_MutableStateRead_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    private static int s_value;

    [EnforcePure]
    public int TestMethod()
    {
        return s_value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_read"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("FieldReferencePurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass.s_value"));
        }

        [Test]
        public async Task Ps0002_StaticPropertyGetterImpurity_PreservesGetterEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int Value
    {
        get
        {
            Console.WriteLine(""impure"");
            return 1;
        }
    }

    [EnforcePure]
    public int TestMethod()
    {
        return Value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.Value.get"));
        }

        [Test]
        public async Task Ps0002_MethodArgumentImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Math.Abs(Console.Read());
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_LinqArgumentImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> values)
    {
        return values.Skip(Console.Read());
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_DirectThrowOnly_IncludesThrowCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("throw"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("ThrowOperationPurityRule"));
        }

        [Test]
        public async Task Ps0009_IsOnlyEmittedWhenExplanationsAreEnabled()
        {
            var source = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine(""impure"");
    }
}";

            var defaultDiagnostics = await GetAnalyzerDiagnosticsAsync(source);
            var explanationDiagnostics = await GetAnalyzerDiagnosticsAsync(
                source,
                ImmutableDictionary<string, string>.Empty.Add("purelysharp_emit_explanations", "true"));

            Assert.That(defaultDiagnostics.Any(d => d.Id == PurelySharpDiagnostics.PurityExplanationId), Is.False);
            Assert.That(explanationDiagnostics.Any(d => d.Id == PurelySharpDiagnostics.PurityExplanationId), Is.True);
        }

        private static Diagnostic SingleDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
        {
            return diagnostics.Single(d => d.Id == diagnosticId);
        }

        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
            string source,
            ImmutableDictionary<string, string>? globalOptions = null)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
            var references = GetTrustedPlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(PurelySharp.Attributes.EnforcePureAttribute).Assembly.Location));

            var compilation = CSharpCompilation.Create(
                "DiagnosticEvidenceTests",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzerOptions = new AnalyzerOptions(
                ImmutableArray<AdditionalText>.Empty,
                new TestAnalyzerConfigOptionsProvider(globalOptions ?? ImmutableDictionary<string, string>.Empty));

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
