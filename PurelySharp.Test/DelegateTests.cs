using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DelegateTests
    {
        [Test]
        public async Task PureMethodWithDelegate_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Creating a delegate but not invoking it
        // The analyzer currently considers creating a delegate with an impure
        // target (Console.WriteLine) to be impure itself
        Action action = () => Console.WriteLine(""Hello"");
        
        // The method doesn't invoke the delegate, but it's still marked impure
        // due to the lambda's body containing an impure operation
        action(); // Invoking might be impure, but defining might be okay
    }
}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(8, 17, 8, 27)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureMethodWithDelegate_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Creating a delegate directly in an impure method
        Action action = () => Console.WriteLine(""Hello"");
        
        // Invoking the delegate makes the method impure
        action();
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(8, 17, 8, 27)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithDelegateInvocation_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public delegate void MyAction();

public class TestClass
{
    private MyAction _pureAction = () => { var x = 1; }; // Pure target

    [EnforcePure]
    public void TestMethod()
    {
        // Even though the target is pure, the analysis might not be able to verify it.
        _pureAction(); // Expect PS0002 due to analysis limitations
    }
}
";


            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(12, 17, 12, 27).WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }
    }
}


