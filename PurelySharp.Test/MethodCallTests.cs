using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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
    public void ImpureHelperMethod()
    {
        Console.WriteLine(""This is impure"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        // Call to impure method should trigger diagnostic, but analyzer doesn't detect it
        ImpureHelperMethod();
    }
}";

            // Expect PMA0002 because ImpureHelperMethod lacks [EnforcePure]
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(18, 9, 18, 29) // Span of ImpureHelperMethod()
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
    public void TestMethod()
    {
        // Pure method call is fine, but console write makes method impure
        int result = PureHelperMethod(5);
        Console.WriteLine(result); // This makes the method impure
    }
}";

            // The analyzer detects the Console.WriteLine in the impure method
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(20, 9, 20, 34).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


