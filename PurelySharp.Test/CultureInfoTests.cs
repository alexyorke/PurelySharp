using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CultureInfoTests
    {
        [Test]
        public async Task CultureInfoCurrentUICulture_Diagnostic()
        {
            var test = @"
using System.Globalization;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public CultureInfo {|PS0002:TestMethod|}()
    {
        return CultureInfo.CurrentUICulture;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
