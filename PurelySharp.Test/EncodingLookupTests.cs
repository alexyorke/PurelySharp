using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EncodingLookupTests
    {
        [Test]
        public async Task EncodingGetEncoding_Diagnostic()
        {
            var test = @"
using System.Text;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Encoding {|PS0002:TestMethod|}()
    {
        return Encoding.GetEncoding(""utf-8"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
