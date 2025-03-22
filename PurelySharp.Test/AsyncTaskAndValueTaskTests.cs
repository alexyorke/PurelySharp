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
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task<int> PureMethod()
    {
        return await Task.FromResult(42);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ValueTask_Constructor_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task ImpureMethod()
    {
        await Task.Run(() => Console.WriteLine(""Hello""));
    }
}";

            var expected = VerifyCS.Diagnostic().WithLocation(13, 9).WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConditionalReturn_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AwaitingPureMethodParameter_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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