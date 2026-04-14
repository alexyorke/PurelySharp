using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Globalization;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class GlobalizationTests
    {



        [Test]
        public async Task CultureInfo_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        // Pure: InvariantCulture is constant
        CultureInfo invariant = CultureInfo.InvariantCulture;
        return invariant.Name;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeParse_InvariantCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string dateStr)
    {
        // DateTime.Parse remains impure even with an explicit culture provider.
        return DateTime.Parse(dateStr, CultureInfo.InvariantCulture);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTime.Parse(dateStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeOffsetParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTimeOffset {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTimeOffset.Parse(dateStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeSpanParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TimeSpan {|PS0002:TestMethod|}(string value)
    {
        return TimeSpan.Parse(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(string numStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }
}";






            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public double {|PS0002:TestMethod|}(string numStr)
    {
        return double.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(double value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(double value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FloatToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(float value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FloatToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(float value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IntToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(int value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IntToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(int value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LongToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(long value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LongToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(long value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ShortToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(short value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ShortToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(short value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ByteToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(byte value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ByteToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(byte value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SByteToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(sbyte value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SByteToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(sbyte value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UShortToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(ushort value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UShortToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(ushort value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UIntToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(uint value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UIntToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(uint value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ULongToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(ulong value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ULongToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(ulong value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HalfToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(Half value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HalfToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(Half value)
    {
        return value.ToString(""G"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DecimalToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(decimal value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DecimalToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(decimal value)
    {
        return value.ToString(""N"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeSpanToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(TimeSpan value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IntParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(string numStr)
    {
        return int.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LongParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long {|PS0002:TestMethod|}(string numStr)
    {
        return long.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BigIntegerParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Numerics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public BigInteger {|PS0002:TestMethod|}(string numStr)
    {
        return BigInteger.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ByteParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte {|PS0002:TestMethod|}(string numStr)
    {
        return byte.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HalfParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Half {|PS0002:TestMethod|}(string numStr)
    {
        return Half.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DecimalParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public decimal {|PS0002:TestMethod|}(string numStr)
    {
        return decimal.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DecimalTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return decimal.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LongTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return long.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ByteTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return byte.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToSingle_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public float {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToSingle(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToSingle_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public float {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToSingle(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDouble_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public double {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToDouble(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDouble_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public double {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToDouble(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDecimal_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public decimal {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToDecimal(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDecimal_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public decimal {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToDecimal(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToByte_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToByte(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToByte_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToByte(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDateTime_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToDateTime(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToDateTime_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToDateTime(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToString_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToString(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToSByte_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public sbyte {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToSByte(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToSByte_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public sbyte {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToSByte(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt32_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToInt32(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt32_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToInt32(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt64_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToInt64(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt64_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToInt64(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt16_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public short {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToInt16(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToInt16_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public short {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToInt16(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt16_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ushort {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToUInt16(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt16_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ushort {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToUInt16(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt32_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public uint {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToUInt32(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt32_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public uint {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToUInt32(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt64_Object_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ulong {|PS0002:TestMethod|}(object value)
    {
        return Convert.ToUInt64(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConvertToUInt64_String_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ulong {|PS0002:TestMethod|}(string value)
    {
        return Convert.ToUInt64(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FloatParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public float {|PS0002:TestMethod|}(string numStr)
    {
        return float.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ShortParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public short {|PS0002:TestMethod|}(string numStr)
    {
        return short.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ShortTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return short.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FloatTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return float.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UShortParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ushort {|PS0002:TestMethod|}(string numStr)
    {
        return ushort.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UShortTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return ushort.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UIntParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public uint {|PS0002:TestMethod|}(string numStr)
    {
        return uint.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UIntTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return uint.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ULongParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ulong {|PS0002:TestMethod|}(string numStr)
    {
        return ulong.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ULongTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return ulong.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SByteParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public sbyte {|PS0002:TestMethod|}(string numStr)
    {
        return sbyte.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SByteTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return sbyte.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HalfTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return Half.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BigIntegerTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Numerics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string numStr)
    {
        return BigInteger.TryParse(numStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTime.TryParse(dateStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeToLongDateString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTime value)
    {
        return value.ToLongDateString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeToLongTimeString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTime value)
    {
        return value.ToLongTimeString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeToShortDateString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTime value)
    {
        return value.ToShortDateString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeToShortTimeString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTime value)
    {
        return value.ToShortTimeString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTime value)
    {
        return value.ToString(""g"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateOnly {|PS0002:TestMethod|}(string dateStr)
    {
        return DateOnly.Parse(dateStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyParseExact_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateOnly {|PS0002:TestMethod|}(string dateStr)
    {
        return DateOnly.ParseExact(dateStr, ""d"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string dateStr)
    {
        return DateOnly.TryParse(dateStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyTryParseExact_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string dateStr)
    {
        return DateOnly.TryParseExact(dateStr, ""d"", out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateOnly value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyToLongDateString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateOnly value)
    {
        return value.ToLongDateString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyToShortDateString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateOnly value)
    {
        return value.ToShortDateString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateOnlyToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateOnly value)
    {
        return value.ToString(""d"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeOffsetTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTimeOffset.TryParse(dateStr, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeOffsetToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(DateTimeOffset value)
    {
        return value.ToString(""g"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeSpanTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string value)
    {
        return TimeSpan.TryParse(value, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TimeOnly {|PS0002:TestMethod|}(string value)
    {
        return TimeOnly.Parse(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyParseExact_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TimeOnly {|PS0002:TestMethod|}(string value)
    {
        return TimeOnly.ParseExact(value, ""t"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyToString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(TimeOnly value)
    {
        return value.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyToLongTimeString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(TimeOnly value)
    {
        return value.ToLongTimeString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyToString_FormatString_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(TimeOnly value)
    {
        return value.ToString(""t"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyTryParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string value)
    {
        return TimeOnly.TryParse(value, out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TimeOnlyTryParseExact_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(string value)
    {
        return TimeOnly.TryParseExact(value, ""t"", out _);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
