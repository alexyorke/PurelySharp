using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class VersionTests
    {
        [Test]
        public async Task VersionConstructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Version TestMethod()
    {
        return new Version(1, 2);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
