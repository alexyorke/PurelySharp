using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class TimeZoneInfoTests
    {
        [Test]
        public async Task TimeZoneInfoLocal_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TimeZoneInfo {|PS0002:TestMethod|}()
    {
        return TimeZoneInfo.Local;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
