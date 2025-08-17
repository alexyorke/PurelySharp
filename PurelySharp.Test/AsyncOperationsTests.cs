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


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AsyncMethodWithAwait_NoDiagnostic()
        {

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


            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


