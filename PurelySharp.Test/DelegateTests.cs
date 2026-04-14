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
        public async Task PureMethodWithDelegate_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

    public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Action action = () => Console.WriteLine(""Hello"");
        action();
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
        Action action = () => Console.WriteLine(""Hello"");
        action();
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(8, 17, 8, 27)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithDelegateInvocation_NoDiagnostic()
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
        _pureAction();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}


