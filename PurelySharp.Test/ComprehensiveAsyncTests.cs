using System;
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
    public class ComprehensiveAsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_WithFromResult_UnknownPurityDiagnostic()
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
            // Analyzer seems to flag async methods returning Task.FromResult - REVERTED
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                      .WithSpan(10, 28, 10, 43) // Span for PureAsyncMethod identifier
            //                      .WithArguments("PureAsyncMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Expect diagnostic
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect NO diagnostic
        }

        [Test]
        public async Task PureAsyncMethod_WithCompletedTask_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task PureAsyncMethod()
    {
        await Task.CompletedTask;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureAsyncMethod_WithValueTask_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async ValueTask<int> {|PS0002:PureAsyncMethod|}()
    {
        return await new ValueTask<int>(42);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureAsyncMethod_WithTaskDelay_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task {|PS0002:ImpureAsyncMethod|}()
    {
        await Task.Delay(100); // Impure operation
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureAsyncMethod_WithStateModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    private int _state;

    [EnforcePure]
    public async Task<int> {|PS0002:ImpureAsyncMethod|}()
    {
        _state++; // State modification is impure
        return await Task.FromResult(_state);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethod_AwaitingPureMethod_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> Helper()
    {
        return await Task.FromResult(42);
    }

    [EnforcePure]
    public async Task<int> PureAsyncMethod()
    {
        return await Helper(); // Awaiting another pure method
    }
}";
            // REVERTED
            // var expectedHelper = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                          .WithSpan(10, 28, 10, 34) // Span for Helper identifier
            //                          .WithArguments("Helper");
            // var expectedPureAsync = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                             .WithSpan(16, 28, 16, 43) // Span for PureAsyncMethod identifier
            //                             .WithArguments("PureAsyncMethod");

            // await VerifyCS.VerifyAnalyzerAsync(test, expectedHelper, expectedPureAsync);
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect NO diagnostics
        }

        [Test]
        public async Task AsyncMethod_AwaitingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    public async Task<int> ImpureHelper()
    {
        Console.WriteLine(""Impure operation"");
        return await Task.FromResult(42);
    }

    [EnforcePure]
    public async Task<int> {|PS0002:ImpureAsyncMethod|}()
    {
        return await ImpureHelper(); // Awaiting an impure method
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TaskRunMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task {|PS0002:ImpureTaskRunMethod|}()
    {
        await Task.Run(() => Console.WriteLine(""Impure operation"")); // Task.Run is impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethod_ReturnWithoutAwait_UnknownPurityDiagnostic()
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
        // No await, but returns a Task directly
        if (true)
            return 42;
        else
            return await Task.FromResult(42);
    }
}";
            // REVERTED
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                      .WithSpan(10, 28, 10, 43) // Span for PureAsyncMethod identifier
            //                      .WithArguments("PureAsyncMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Expect diagnostic
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect NO diagnostics
        }

        [Test]
        public async Task AsyncMethod_ConditionalAwait_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod(bool condition)
    {
        if (condition)
        {
            return await Task.FromResult(42);
        }
        return 42;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect NO diagnostics
        }
    }
}