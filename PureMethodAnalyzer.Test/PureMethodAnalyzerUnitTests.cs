using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.MSTest.CodeFixVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer,
    PureMethodAnalyzer.EnforcePureMethodAnalyzerCodeFixProvider>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class PureMethodAnalyzerUnitTest
    {
        // Test that a method marked with [EnforcePure] and having a non-empty body produces a diagnostic.
        [TestMethod]
        public async Task MethodWithNonEmptyBody_ShouldReportDiagnostic()
        {
            var testCode = @"
using System;
using MyPurityEnforcement;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public void {|#0:TestMethod|}()
        {
            Console.WriteLine(""Impure"");
        }
    }
}

namespace MyPurityEnforcement
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class EnforcePureAttribute : System.Attribute
    {
    }
}
";

            var expectedDiagnostic = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(0)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        // Test that a method marked with [EnforcePure] and having an empty body does not produce a diagnostic.
        [TestMethod]
        public async Task MethodWithEmptyBody_ShouldNotReportDiagnostic()
        {
            var testCode = @"
using System;
using MyPurityEnforcement;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public void TestMethod()
        {
        }
    }
}

namespace MyPurityEnforcement
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class EnforcePureAttribute : System.Attribute
    {
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // Test that a method not marked with [EnforcePure] does not produce a diagnostic even if it has a non-empty body.
        [TestMethod]
        public async Task MethodWithoutEnforcePure_ShouldNotReportDiagnostic()
        {
            var testCode = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Impure"");
        }
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // Test that the code fix makes the method body empty for methods marked with [EnforcePure].
        [TestMethod]
        public async Task CodeFix_ShouldMakeMethodBodyEmpty()
        {
            var testCode = @"
using System;
using MyPurityEnforcement;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public void {|#0:TestMethod|}()
        {
            Console.WriteLine(""Impure"");
        }
    }
}

namespace MyPurityEnforcement
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class EnforcePureAttribute : System.Attribute
    {
    }
}
";

            var fixedCode = @"
using System;
using MyPurityEnforcement;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public void TestMethod()
        {
        }
    }
}

namespace MyPurityEnforcement
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    public sealed class EnforcePureAttribute : System.Attribute
    {
    }
}
";

            var expectedDiagnostic = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(0)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
        }
    }
}
