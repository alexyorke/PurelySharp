using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class GuidTests
    {
        [Test]
        public async Task GuidNewGuid_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Guid {|PS0002:TestMethod|}()
    {
        return Guid.NewGuid();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
