using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BinaryPrimitivesTests
    {
        [Test]
        public async Task BinaryPrimitivesIntegerReads_NoDiagnostic()
        {
            var test = @"
using System.Buffers.Binary;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long TestMethod(byte[] data)
    {
        return
            BinaryPrimitives.ReadInt16BigEndian(data) +
            BinaryPrimitives.ReadInt16LittleEndian(data) +
            BinaryPrimitives.ReadInt32BigEndian(data) +
            BinaryPrimitives.ReadInt32LittleEndian(data) +
            BinaryPrimitives.ReadInt64BigEndian(data) +
            BinaryPrimitives.ReadInt64LittleEndian(data) +
            BinaryPrimitives.ReadUInt16BigEndian(data) +
            BinaryPrimitives.ReadUInt16LittleEndian(data) +
            BinaryPrimitives.ReadUInt32BigEndian(data) +
            BinaryPrimitives.ReadUInt32LittleEndian(data) +
            (long)BinaryPrimitives.ReadUInt64BigEndian(data) +
            (long)BinaryPrimitives.ReadUInt64LittleEndian(data);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BinaryPrimitivesReverseEndianness_NoDiagnostic()
        {
            var test = @"
using System.Buffers.Binary;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long TestMethod(short s, ushort us, int i, uint ui, long l, ulong ul)
    {
        return
            BinaryPrimitives.ReverseEndianness(s) +
            BinaryPrimitives.ReverseEndianness(us) +
            BinaryPrimitives.ReverseEndianness(i) +
            BinaryPrimitives.ReverseEndianness(ui) +
            BinaryPrimitives.ReverseEndianness(l) +
            (long)BinaryPrimitives.ReverseEndianness(ul);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestCase("BinaryPrimitives.WriteInt16BigEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteInt16LittleEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteInt32BigEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteInt32LittleEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteInt64BigEndian(destination, 1L);")]
        [TestCase("BinaryPrimitives.WriteInt64LittleEndian(destination, 1L);")]
        [TestCase("BinaryPrimitives.WriteUInt16BigEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteUInt16LittleEndian(destination, 1);")]
        [TestCase("BinaryPrimitives.WriteUInt32BigEndian(destination, 1U);")]
        [TestCase("BinaryPrimitives.WriteUInt32LittleEndian(destination, 1U);")]
        [TestCase("BinaryPrimitives.WriteUInt64LittleEndian(destination, 1UL);")]
        public async Task BinaryPrimitivesIntegerWrites_Diagnostic(string statement)
        {
            var test = @"
using System;
using System.Buffers.Binary;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<byte> destination)
    {
        " + statement + @"
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestCase("BinaryPrimitives.WriteSingleBigEndian(destination, 1.0f);")]
        [TestCase("BinaryPrimitives.WriteSingleLittleEndian(destination, 1.0f);")]
        [TestCase("BinaryPrimitives.WriteDoubleBigEndian(destination, 1.0);")]
        [TestCase("BinaryPrimitives.WriteDoubleLittleEndian(destination, 1.0);")]
        [TestCase("BinaryPrimitives.WriteHalfBigEndian(destination, (Half)1);")]
        [TestCase("BinaryPrimitives.WriteHalfLittleEndian(destination, (Half)1);")]
        public async Task BinaryPrimitivesFloatingPointWrites_Diagnostic(string statement)
        {
            var test = @"
using System;
using System.Buffers.Binary;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<byte> destination)
    {
        " + statement + @"
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
