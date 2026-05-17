using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class WebUtilityTests
    {
        [Test]
        public async Task WebUtilityHtmlEncode_NoDiagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WebUtilityUrlDecode_NoDiagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string value)
    {
        return WebUtility.UrlDecode(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WebUtilityHtmlDecode_NoDiagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string value)
    {
        return WebUtility.HtmlDecode(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WebUtilityUrlEncode_NoDiagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string value)
    {
        return WebUtility.UrlEncode(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WebUtilityUrlEncodeToBytes_ReturnedArray_Diagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(byte[] value)
    {
        return WebUtility.UrlEncodeToBytes(value, 0, value.Length);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WebUtilityUrlDecodeToBytes_ReturnedArray_Diagnostic()
        {
            var test = @"
using System.Net;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(byte[] value)
    {
        return WebUtility.UrlDecodeToBytes(value, 0, value.Length);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
