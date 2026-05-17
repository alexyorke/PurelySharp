using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UriTests
    {
        [Test]
        public async Task UriIsWellFormedUriString_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(string value)
    {
        return Uri.IsWellFormedUriString(value, UriKind.Absolute);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
