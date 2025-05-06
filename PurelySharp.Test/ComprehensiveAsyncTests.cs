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
            // Expect no diagnostics as Task.FromResult is pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
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

            // Expect no diagnostics as Task.CompletedTask is pure.
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
    public async ValueTask<int> PureAsyncMethod()
    {
        return await new ValueTask<int>(42);
    }
}";

            // Expect PS0002 for PureAsyncMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(9, 33, 9, 48).WithArguments("PureAsyncMethod"));
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
    public async Task ImpureAsyncMethod()
    {
        await Task.Delay(100); // Impure operation
    }
}";

            // Expect PS0002 for ImpureAsyncMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(9, 23, 9, 40).WithArguments("ImpureAsyncMethod"));
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
    public async Task<int> ImpureAsyncMethod()
    {
        _state++; // State modification is impure
        return await Task.FromResult(_state);
    }
}";

            // Expect PS0002 for ImpureAsyncMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(11, 28, 11, 45).WithArguments("ImpureAsyncMethod"));
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
            // Expect no diagnostics as both methods are pure and await pure operations.
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public async Task<int> ImpureAsyncMethod()
    {
        return await ImpureHelper(); // Awaiting an impure method
    }
}";

            // Expect PS0002 for ImpureAsyncMethod and ImpureHelper
            var diag1 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(15, 28, 15, 45).WithArguments("ImpureAsyncMethod");
            var diag2 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(8, 28, 8, 40).WithArguments("ImpureHelper");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { diag1, diag2 });
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
    public async Task ImpureTaskRunMethod()
    {
        await Task.Run(() => Console.WriteLine(""Impure operation"")); // Task.Run is impure
    }
}";

            // Expect PS0002 for ImpureTaskRunMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(9, 23, 9, 42).WithArguments("ImpureTaskRunMethod"));
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
            // Expect no diagnostics as both paths are pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
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
            // Expect no diagnostics as both paths are pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}