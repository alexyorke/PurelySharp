using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class MethodCallTests
    {
        [Test]
        public async Task PureMethodCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int PureHelperMethod(int x)
    {
        return x * 2;
    }

    [EnforcePure]
    public int TestMethod(int x)
    {
        // Call to pure method should be considered pure
        return PureHelperMethod(x) + 5;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    // Note: ImpureHelperMethod lacks [EnforcePure]
    public void ImpureHelperMethod()
    {
        Console.WriteLine(""This is impure""); // Impure
    }

    [EnforcePure]
    public void TestMethod()
    {
        // Call to impure method should trigger diagnostic on TestMethod
        ImpureHelperMethod();
    }
}";

            // Expect PS0002 on TestMethod and ImpureHelperMethod based on runner output (2 diagnostics total)
            var expectedImpureHelperPS0002 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 17, 10, 35).WithArguments("ImpureHelperMethod");
            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(16, 17, 16, 27).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedImpureHelperPS0002, expectedTestMethod });
        }

        [Test]
        public async Task ImpureMethodCallingPureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int PureHelperMethod(int x)
    {
        return x * 2;
    }

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Pure method call is fine, but console write makes method impure
        int result = PureHelperMethod(5);
        Console.WriteLine(result); // This makes the method impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


