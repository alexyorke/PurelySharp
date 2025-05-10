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
        public async Task<int> PureAsyncMethod()
        {
            await Task.Delay(10);
            return 42;
        }
    }
}";

            // Expect PS0002 for PureAsyncMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(10, 32, 10, 47).WithArguments("PureAsyncMethod"));
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
        public async Task ImpureAsyncMethod()
        {
            await Task.Delay(10);
            File.WriteAllText(""temp.txt"", ""impure write"");
        }
    }
}";

            // Expect PS0002 for ImpureAsyncMethod
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(11, 27, 11, 44).WithArguments("ImpureAsyncMethod"));
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
        public async Task<int> MethodCallingImpureAsync()
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

            // Expect PS0002 for MethodCallingImpureAsync. GetValueAsync is not marked.
            var expectedOuter = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(10, 32, 10, 56).WithArguments("MethodCallingImpureAsync");
            // var expectedInner = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(16, 33, 16, 46).WithArguments("GetValueAsync"); // Removed: Not marked

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedOuter });
        }
    }
}


