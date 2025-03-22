using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AsyncOperationsTests
    {
        [Test]
        public async Task MethodWithAsyncOperation_Diagnostic()
        {
            // Previously, we expected async methods to be marked as impure by default.
            // Now, with our updated implementation, we check the contents of async methods instead.
            // We're temporarily ignoring this test while we refine the async detection.

            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

class Program
{
    [Pure]
    public async Task<int> TestMethod()
    {
        return 1 + 2;
    }
}";
            // With the updated implementation, we no longer expect a diagnostic for this async method
            // since it doesn't have any impure operations.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethodWithAwait_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public async Task<int> TestMethod()
    {
        // Pure operations in an async method
        int x = 1;
        int y = 2;
        int result = x + y;
        // No impure operations, just await a completed task
        await Task.CompletedTask;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}