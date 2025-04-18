using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod()
    {
        return await Task.FromResult(42);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(13, 22, 13, 41)
                .WithArguments("PureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureAsyncMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    private int _counter = 0;

    [EnforcePure]
    public async Task<int> ImpureAsyncMethod()
    {
        _counter++;
        return await Task.FromResult(_counter);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(15, 9, 15, 19).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureAsyncMethodWithAwait_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> ImpureAsyncMethod()
    {
        // Task.Delay is impure as it involves timing operations
        await Task.Delay(100);
        return 42;
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(14, 9, 14, 30).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


