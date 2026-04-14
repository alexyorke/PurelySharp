using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class InteropTests
    {
        [Test]
        public async Task SafeHandleIsInvalid_Diagnostic()
        {
            var test = @"
using System.Runtime.InteropServices;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(SafeHandle handle)
    {
        return handle.IsInvalid;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }










    }
}
