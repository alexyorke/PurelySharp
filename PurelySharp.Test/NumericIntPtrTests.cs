using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NumericIntPtrTests
    {
        [Test]
        public async Task NativeInt_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class NativeIntCalculator
    {
        [EnforcePure]
        public nint Add(nint a, nint b)
        {
            // C# 11 feature: Native-sized integer arithmetic
            return a + b;
        }
        
        [EnforcePure]
        public nint Multiply(nint a, nint b)
        {
            // C# 11 feature: Native-sized integer arithmetic with other operations
            return a * b;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedNativeInt_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedNativeIntCalculator
    {
        [EnforcePure]
        public nuint Add(nuint a, nuint b)
        {
            // C# 11 feature: Unsigned native-sized integer arithmetic
            return a + b;
        }
        
        [EnforcePure]
        public nuint Multiply(nuint a, nuint b)
        {
            // C# 11 feature: Unsigned native-sized integer arithmetic with other operations
            return a * b;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithConversions_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class NativeIntConverter
    {
        [EnforcePure]
        public nint ConvertToNInt(int value)
        {
            // C# 11 feature: Conversion between int and nint
            return (nint)value;
        }
        
        [EnforcePure]
        public int ConvertToInt(nint value)
        {
            // C# 11 feature: Conversion between nint and int
            return (int)value;
        }
        
        [EnforcePure]
        public nuint ConvertToNUInt(uint value)
        {
            // C# 11 feature: Conversion between uint and nuint
            return (nuint)value;
        }
        
        [EnforcePure]
        public uint ConvertToUInt(nuint value)
        {
            // C# 11 feature: Conversion between nuint and uint
            return (uint)value;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithComparisons_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class NativeIntComparer
    {
        [EnforcePure]
        public bool IsGreaterThan(nint a, nint b)
        {
            // C# 11 feature: Comparison operations with native int
            return a > b;
        }
        
        [EnforcePure]
        public bool IsLessThan(nint a, nint b)
        {
            // C# 11 feature: Comparison operations with native int
            return a < b;
        }
        
        [EnforcePure]
        public bool AreEqual(nint a, nint b)
        {
            // C# 11 feature: Equality operations with native int
            return a == b;
        }
        
        [EnforcePure]
        public bool IsGreaterThan(nuint a, nuint b)
        {
            // C# 11 feature: Comparison operations with unsigned native int
            return a > b;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithConstants_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class NativeIntConstants
    {
        [EnforcePure]
        public nint GetLargePositiveValue()
        {
            // C# 11 feature: Using large values for nint
            return (nint)1000000000;
        }
        
        [EnforcePure]
        public nint GetNegativeValue()
        {
            // C# 11 feature: Using negative nint values
            return (nint)(-1000000000);
        }
        
        [EnforcePure]
        public nuint GetLargeUnsignedValue()
        {
            // C# 11 feature: Using large values for nuint
            return (nuint)4000000000;
        }
        
        [EnforcePure]
        public nuint GetZeroValue()
        {
            // C# 11 feature: Using zero nuint value
            return (nuint)0;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithBitwiseOperations_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class NativeIntBitwise
    {
        [EnforcePure]
        public nint BitwiseAnd(nint a, nint b)
        {
            // C# 11 feature: Bitwise operations with native int
            return a & b;
        }
        
        [EnforcePure]
        public nint BitwiseOr(nint a, nint b)
        {
            // C# 11 feature: Bitwise operations with native int
            return a | b;
        }
        
        [EnforcePure]
        public nint BitwiseXor(nint a, nint b)
        {
            // C# 11 feature: Bitwise operations with native int
            return a ^ b;
        }
        
        [EnforcePure]
        public nint BitwiseNot(nint a)
        {
            // C# 11 feature: Bitwise operations with native int
            return ~a;
        }
        
        [EnforcePure]
        public nint LeftShift(nint a, int shift)
        {
            // C# 11 feature: Shift operations with native int
            return a << shift;
        }
        
        [EnforcePure]
        public nint RightShift(nint a, int shift)
        {
            // C# 11 feature: Shift operations with native int
            return a >> shift;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



namespace TestNamespace
{
    public class NativeIntLogger
    {
        [EnforcePure]
        public void LogNativeInt(nint value)
        {
            // Impure operation with native int
            File.WriteAllText(""log.txt"", value.ToString());
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(16, 13, 16, 59)
                    .WithArguments("LogNativeInt")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


