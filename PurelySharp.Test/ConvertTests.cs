using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConvertTests
    {
        [Test]
        public async Task ConvertFromBase64String_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(string value)
    {
        return Convert.FromBase64String(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertFromBase64String_LocalNonEscapingUse_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(string value)
    {
        var bytes = Convert.FromBase64String(value);
        return bytes.Length;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
