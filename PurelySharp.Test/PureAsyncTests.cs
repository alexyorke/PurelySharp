using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class PureAsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

class Program
{
    [Pure]
    public async Task<int> PureAsyncMethod()
    {
        // Simple async method with no side effects
        return await Task.FromResult(42); // Changed to Task.FromResult which is guaranteed pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureAsyncMethod_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

class Program
{
    private int _counter = 0;

    [Pure]
    public async Task<int> ImpureAsyncMethod()
    {
        // Has side effects - modifies field
        await Task.Delay(1);
        _counter++;
        return _counter;
    }
}";

            var expected = VerifyCS.Diagnostic().WithSpan(17, 9, 17, 19).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}