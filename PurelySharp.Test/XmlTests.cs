using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class XmlTests
    {
        [Test]
        public async Task XmlDocumentLoadXml_Diagnostic()
        {
            var test = @"
using System.Xml;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public XmlDocument {|PS0002:TestMethod|}(XmlDocument document)
    {
        document.LoadXml(""<root />"");
        return document;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
