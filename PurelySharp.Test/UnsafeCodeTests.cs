// #if false // Temporarily disable this class
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
using Microsoft.CodeAnalysis.CSharp.Testing; // For CSharpParseOptions

namespace PurelySharp.Test
{
    [TestFixture]
    // [NUnit.Framework.Skip("Skipping for now")] // Removed skip attribute
    public class UnsafeCodeTests
    {
        // Minimal attribute definition (using verbatim string)
        private const string MinimalEnforcePureAttributeSource = @"
using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.All)] // Use permissive usage for testing
    public sealed class EnforcePureAttribute : Attribute { }
}
";

        [Test]
        public async Task MethodWithUnsafeCode_Diagnostic()
        {
            // Main test code without attribute definition concatenated
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
}
";

            // Expect 1 diagnostic
            var expected = new[] {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                       .WithSpan("/0/Test1.cs", 8, 24, 8, 34) // Use Test1.cs based on actual output
                                       .WithArguments("TestMethod")
            };

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    // Provide attribute source separately
                    Sources = { MinimalEnforcePureAttributeSource, test },
                    ExpectedDiagnostics = { expected[0] },
                },
            };

            // Enable unsafe code and set LanguageVersion via SolutionTransforms
            verifierTest.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                if (project == null) return solution;

                var parseOptions = (project.ParseOptions as CSharpParseOptions)?
                    .WithLanguageVersion(LanguageVersion.Latest);
                // Explicitly check if original options were null before passing
                solution = solution.WithProjectParseOptions(projectId, parseOptions ?? project.ParseOptions ?? new CSharpParseOptions());

                var compilationOptions = (project.CompilationOptions as CSharpCompilationOptions)?
                    .WithAllowUnsafe(true);
                // Explicitly check if original options were null before passing
                solution = solution.WithProjectCompilationOptions(projectId, compilationOptions ?? project.CompilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                return solution;
            });
            await verifierTest.RunAsync();
        }

        [Test]
        public async Task MethodWithFixedStatement_Diagnostic()
        {
            // Main test code without attribute definition concatenated
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public static unsafe void TestMethod()
    {
        byte[] array = new byte[10];
        fixed (byte* ptr = array)
        {
            *ptr = 42;
        }
    }
}
";

            // Expect only 1 diagnostic on the method definition
            var expected = new[] {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                    .WithSpan("/0/Test1.cs", 8, 31, 8, 41) // Use Test1.cs based on actual output
                    .WithArguments("TestMethod")
            };

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                     // Provide attribute source separately
                    Sources = { MinimalEnforcePureAttributeSource, test },
                    ExpectedDiagnostics = { expected[0] },
                },
            };

            // Enable unsafe code and set LanguageVersion via SolutionTransforms
            verifierTest.SolutionTransforms.Add((solution, projectId) =>
           {
               var project = solution.GetProject(projectId);
               if (project == null) return solution;

               var parseOptions = (project.ParseOptions as CSharpParseOptions)?
                   .WithLanguageVersion(LanguageVersion.Latest);
               // Explicitly check if original options were null before passing
               solution = solution.WithProjectParseOptions(projectId, parseOptions ?? project.ParseOptions ?? new CSharpParseOptions());

               var compilationOptions = (project.CompilationOptions as CSharpCompilationOptions)?
                   .WithAllowUnsafe(true);
               // Explicitly check if original options were null before passing
               solution = solution.WithProjectCompilationOptions(projectId, compilationOptions ?? project.CompilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

               return solution;
           });

            await verifierTest.RunAsync();
        }
    }
}
// #endif // Temporarily disable this class


