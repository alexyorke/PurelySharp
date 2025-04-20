using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // Need this for [EnforcePure]
using System.IO; // Added for potential reference resolution
using Microsoft.CodeAnalysis; // Added for MetadataReference
using System.Collections.Immutable; // Added for ImmutableArray

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicPurityTests
    {
        [Test]
        public async Task TestPureMethod_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes; // Assuming attributes are in this namespace

public class TestClass
{
    [Pure]
    public int GetConstant()
    {
        return 42;
    }
}";

            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureMethod_ShouldBeFlaggedNow()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:GetConstant|}() // Explicitly marked for PS0002
    {
        return 5;
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestImpureMethod_ShouldBeFlagged_OnMethodName()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    private static int _counter = 0;

    [EnforcePure]
    public int {|PS0002:ImpureMethod|}() // Explicitly marked for PS0002
    {
        _counter++; // Modifies static state
        return _counter;
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        // Renamed test and updated expectations
        public async Task TestEnforcePureOnNonMethod_ReportsMisplacedAttribute()
        {
            // Add the attribute source code directly to the test source
            var attributeSource = @"
using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.All)] 
    public sealed class EnforcePureAttribute : Attribute { }
}";

            // Reverted spans back to covering only the attribute
            var testCodeWithMarkup = @"
using PurelySharp.Attributes;
using System;

[{|PS0003:EnforcePure|}] // On class - Should report PS0003 on attribute
public class TestClass
{
    [{|PS0003:EnforcePure|}] // On field - Should report PS0003 on attribute
    private int _field = 0;

    [{|PS0003:EnforcePure|}] // On property - Should report PS0003 on attribute
    public int MyProperty { get; set; }

    // Valid method with attribute - PS0002 not expected in *this* test
    [EnforcePure]
    public void ValidMethod() { } // Ensure no markup here

    // Method without attribute - Should be ignored
    public void AnotherMethod() { }
}";

            // Configure the test runner
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, testCodeWithMarkup }, // Include both sources
                },
                // Remove explicit expected diagnostics, rely on markup
                /*
                ExpectedDiagnostics =
                {
                    // Span locations match the {|PS0003:...|} markup in the source
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(5, 2, 5, 13),
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(8, 6, 8, 17),
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(11, 6, 11, 17),
                    // NOTE: PS0002 is intentionally omitted here
                },
                */
                ReferenceAssemblies = Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net80,
                // No need to disable CS0592 anymore as AttributeUsage allows other targets.
                // We might still disable warnings like CS1591 or CS0414 if they appear.
                // CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.Warnings, 
                // DisabledDiagnostics = { "CS1591", "CS0414" } 
            };

            // Re-add the explicit metadata reference transform even though source is included
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                solution = solution.AddMetadataReference(projectId, 
                    MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location));
                return solution;
            });

            // The verifier will automatically check the diagnostics specified in the markup
            await test.RunAsync();
        }
    }
} 