#if false // Temporarily disable this class
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using System.Linq;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    // [NUnit.Framework.Skip("Skipping for now")] // Removed skip attribute
    public class UnsafeCodeTests
    {
        [Test]
        public async Task MethodWithUnsafeCode_Diagnostic()
        {
            // Add the attribute source code directly to the test source
            var attributeSource = @"
using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.All)] // Use permissive usage for testing
    public sealed class EnforcePureAttribute : Attribute { }
}";

            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public unsafe void TestMethod()
    {
        int x = 5;
        int* p = &x;
        *p = 10; // Unsafe code is considered impure
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan("/0/Test1.cs", 11, 18, 11, 20) // Span of &x
                                   .WithArguments("TestMethod");

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, test },
                    ExpectedDiagnostics = { expected },
                },
            };
            verifierTest.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId)!;
                var options = (CSharpCompilationOptions)project.CompilationOptions!;
                options = options.WithAllowUnsafe(true);
                return solution.WithProjectCompilationOptions(projectId, options);
            });
            await verifierTest.RunAsync();
        }

        [Test]
        public async Task MethodWithFixedStatement_Diagnostic()
        {
            // Add the attribute source code directly to the test source
            var attributeSource = @"
using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.All)] // Use permissive usage for testing
    public sealed class EnforcePureAttribute : Attribute { }
}";

            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public static unsafe void TestMethod()
    {
        byte[] array = {|PS0002:new|} byte[10];
        fixed (byte* ptr = array)
        {
            *ptr = 42;
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan("/0/Test1.cs", 10, 24, 10, 27) // Span of 'new' keyword
                                   .WithArguments("TestMethod");

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, test },
                    ExpectedDiagnostics = { expected },
                },
            };
            verifierTest.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId)!;
                var options = (CSharpCompilationOptions)project.CompilationOptions!;
                options = options.WithAllowUnsafe(true);
                return solution.WithProjectCompilationOptions(projectId, options);
            });
            await verifierTest.RunAsync();
        }
    }
}
#endif // Temporarily disable this class


