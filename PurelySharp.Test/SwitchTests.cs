using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class SwitchTests
    {
        [Test]
        public async Task PureMethodWithSwitch_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int value)
    {
        switch (value)
        {
            case 1:
                return 10;
            case 2:
                return 20;
            case 3:
                return 30;
            default:
                return 0;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithSwitch_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _state;

    [EnforcePure]
    public int TestMethod(int value)
    {
        switch (value)
        {
            case 1:
                _state++; // Impure operation
                return 10;
            case 2:
                return 20;
            default:
                return 0;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(17, 17, 17, 25).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithSwitchAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int value)
    {
        switch (value)
        {
            case 1:
                Console.WriteLine(""Case 1""); // Impure operation
                return 10;
            case 2:
                return 20;
            default:
                return 0;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(15, 17, 15, 44).WithArguments("TestMethod"));
        }
    }
}


