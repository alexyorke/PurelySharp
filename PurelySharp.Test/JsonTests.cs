using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class JsonTests
    {
        [Test]
        public async Task JsonDocumentParse_Diagnostic()
        {
            var test = @"
using System.Text.Json;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public JsonDocument {|PS0002:TestMethod|}()
    {
        return JsonDocument.Parse(""{}"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task JsonElementGetString_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Text.Json;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(JsonElement element)
    {
        return element.GetString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
