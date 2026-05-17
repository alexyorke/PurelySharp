using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BitConverterTests
    {
        [Test]
        public async Task BitConverterGetBytesInt_ReturnedArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(int value)
    {
        return BitConverter.GetBytes(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BitConverterGetBytesInt_LocalReturnedArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        return bytes;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BitConverterGetBytesInt_ConditionalReturnedArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(bool useLeft, int value)
    {
        return useLeft
            ? BitConverter.GetBytes(value)
            : BitConverter.GetBytes(value + 1);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
