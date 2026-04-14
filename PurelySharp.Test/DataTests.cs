using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DataTests
    {
        [Test]
        public async Task DataColumnConstructor_Diagnostic()
        {
            var test = @"
using System.Data;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DataColumn {|PS0002:TestMethod|}()
    {
        return new DataColumn(""Id"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
