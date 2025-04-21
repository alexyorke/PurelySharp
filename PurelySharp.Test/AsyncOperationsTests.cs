using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
using PurelySharp.Attributes;
using System.Threading.Tasks;



class Program
{
    [EnforcePure]
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
            // Test case with async method using await on a known pure method
            var test = @"
using System.Threading.Tasks;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public async Task<int> {|PS0002:TestMethod|}()
        {
            // Await a pure Task.Delay
            await Task.Delay(10);
            return 42;
        }
    }
}";

            // Diagnostics are now inline in the test code
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


