using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class NetworkingTests
    {
        [Test]
        public async Task HttpContentHeadersContentLength_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Net.Http.Headers;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long? {|PS0002:TestMethod|}(HttpContentHeaders headers)
    {
        return headers.ContentLength;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HttpResponseMessageIsSuccessStatusCode_Diagnostic()
        {
            var test = @"
using System.Net.Http;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(HttpResponseMessage response)
    {
        return response.IsSuccessStatusCode;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HttpRequestMessageConstructor_Diagnostic()
        {
            var test = @"
using System.Net.Http;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public HttpRequestMessage {|PS0002:TestMethod|}()
    {
        return new HttpRequestMessage(HttpMethod.Get, ""https://example.com"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }











    }
}
