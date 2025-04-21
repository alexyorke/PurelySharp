using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

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
    public int {|PS0002:TestMethod|}(int value)
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
    public int {|PS0002:TestMethod|}(int value)
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

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int {|PS0002:TestMethod|}(int value)
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


