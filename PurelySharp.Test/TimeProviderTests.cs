using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class TimeProviderTests
    {
        [Test]
        public async Task TimeProviderSystemGetUtcNow_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTimeOffset {|PS0002:TestMethod|}()
    {
        return TimeProvider.System.GetUtcNow();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
