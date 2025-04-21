using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
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
    public unsafe void {|PS0002:TestMethod|}()
    {
        int x = 5;
        int* p = &x;
        *p = 10; // Unsafe code is considered impure
    }
}";

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, test }, // Add both sources
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
    public unsafe void {|PS0002:TestMethod|}()
    {
        byte[] array = new byte[10];
        fixed (byte* ptr = array)
        {
            *ptr = 42;
        }
    }
}";

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, test }, // Add both sources
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


