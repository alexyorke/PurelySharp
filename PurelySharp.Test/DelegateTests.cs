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
        public async Task DelegateWithImpureTarget_Diagnostic()
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

        [Test]
        public async Task DelegateInvocationWithImpureArgument_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public static int PureTarget(int value)
    {
        return value;
    }

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        Func<int, int> projector = PureTarget;
        return projector(Console.Read());
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task DelegateReassignedToUnknownTarget_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public static void PureTarget()
    {
    }

    [EnforcePure]
    public void {|PS0002:TestMethod|}(Action unknown)
    {
        Action action = PureTarget;
        action = unknown;
        action();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task DelegateReassignedToUnknownTargetOnOneBranch_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public static void PureTarget()
    {
    }

    [EnforcePure]
    public void {|PS0002:TestMethod|}(bool flag, Action unknown)
    {
        Action action = PureTarget;
        if (flag)
        {
            action = unknown;
        }

        action();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}


