using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class YieldTests
    {
        [Test]
        public async Task PureMethodWithYield_Diagnostic()
        {
            // Expectation limitation: Analyzer considers any method using 'yield return' as impure,
            // even if the yielded values are pure.
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> GetNumbers()
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(9, 29, 9, 39)
                                   .WithArguments("GetNumbers");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task ImpureMethodWithYield_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    private int _state = 0;

    [EnforcePure]
    public IEnumerable<int> GetNumbers()
    {
        _state++;
        yield return _state;
    }
}";
            var expected = VerifyCS.Diagnostic("PS0002")
                                   .WithSpan(11, 29, 11, 39)
                                   .WithArguments("GetNumbers");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithYieldAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> GetNumbers()
    {
        Console.WriteLine(""Generating numbers"");
        yield return 1;
        yield return 2;
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(9, 29, 9, 39)
                                   .WithArguments("GetNumbers");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }
    }
}


