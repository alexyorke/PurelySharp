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
    public class AsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_UnknownPurityDiagnostic()
        {
            var test = @"
using System.Threading.Tasks;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public async Task<int> {|PS0002:PureAsyncMethod|}()
        {
            await Task.Delay(10);
            return 42;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureAsyncMethod_Diagnostic()
        {
            var test = @"
using System.Threading.Tasks;
using System.IO;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public async Task {|PS0002:ImpureAsyncMethod|}()
        {
            await Task.Delay(10);
            File.WriteAllText(""temp.txt"", ""impure write"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureAsyncMethodWithAwait_Diagnostic()
        {
            var test = @"
using System.Threading.Tasks;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public async Task<int> {|PS0002:MethodCallingImpureAsync|}()
        {
            int result = await GetValueAsync();
            return result + 1;
        }

        private async Task<int> GetValueAsync()
        {
            await Task.Delay(5);
            return 100;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


