using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ComprehensiveAsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_WithFromResult_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod()
    {
        return await Task.FromResult(42);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureAsyncMethod_WithCompletedTask_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async ValueTask<int> PureAsyncMethod()
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
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task ImpureAsyncMethod()
    {
        await Task.Delay(100); // Impure operation
    }
}";

            var expected = VerifyCS.Diagnostic().WithSpan(13, 9, 13, 30).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureAsyncMethod_WithStateModification_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            var expected = VerifyCS.Diagnostic().WithSpan(15, 9, 15, 17).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task AsyncMethod_AwaitingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethod_AwaitingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            var expected = VerifyCS.Diagnostic().WithSpan(19, 16, 19, 36).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task TaskRunMethod_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task ImpureTaskRunMethod()
    {
        await Task.Run(() => Console.WriteLine(""Impure operation"")); // Task.Run is impure
    }
}";

            var expected = VerifyCS.Diagnostic().WithSpan(13, 9, 13, 68).WithArguments("ImpureTaskRunMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task AsyncMethod_ReturnWithoutAwait_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethod_ConditionalAwait_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod(bool condition)
    {
        if (condition)
            return await Task.FromResult(42);
        else
            return 42;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}