using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DateTimeTests
    {
        [Test]
        public async Task DateTimeToday_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}()
    {
        return DateTime.Today;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
