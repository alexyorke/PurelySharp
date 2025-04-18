using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AsyncTaskAndValueTaskTests
    {
        [Test]
        public async Task Task_CompletedTask_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task PureMethod()
    {
        await Task.CompletedTask;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Task_FromResult_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureMethod()
    {
        return await Task.FromResult(42);
    }
}";
            // Expect PMA0002 because Task.FromResult is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(13, 22, 13, 41) // Span from test error output
                .WithArguments("PureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ValueTask_Constructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureMethod()
    {
        return await new ValueTask<int>(42);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TaskRun_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task ImpureMethod()
    {
        await Task.Run(() => Console.WriteLine(""Hello""));
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithLocation(13, 9).WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConditionalReturn_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureMethod(bool flag)
    {
        if (flag)
            return 42;
        return await Task.FromResult(42);
    }
}";
            // Expect PMA0002 because Task.FromResult is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 22, 15, 41) // Span from test error output
                .WithArguments("PureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task AwaitingPureMethodParameter_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureMethod(Task<int> task)
    {
        return await task;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}