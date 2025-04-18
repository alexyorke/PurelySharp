using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UnsignedRightShiftTests
    {
        [Test]
        public async Task UnsignedRightShift_IntegerTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedRightShiftTest
    {
        [EnforcePure]
        public int UnsignedRightShiftInt(int value, int shift)
        {
            // Pure operation with >>> operator
            return value >>> shift;
        }

        [EnforcePure]
        public uint UnsignedRightShiftUInt(uint value, int shift)
        {
            // Pure operation with >>> operator on unsigned int
            return value >>> shift;
        }

        [EnforcePure]
        public long UnsignedRightShiftLong(long value, int shift)
        {
            // Pure operation with >>> operator on long
            return value >>> shift;
        }

        [EnforcePure]
        public ulong UnsignedRightShiftULong(ulong value, int shift)
        {
            // Pure operation with >>> operator on unsigned long
            return value >>> shift;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedRightShift_WithVariables_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedRightShiftVariablesTest
    {
        [EnforcePure]
        public int UnsignedRightShiftWithVariables(int value, int shift)
        {
            int result = value;
            result = result >>> shift;
            return result;
        }

        [EnforcePure]
        public int ChainedUnsignedRightShift(int value, int shift1, int shift2)
        {
            // Chained use of >>> operator
            return (value >>> shift1) >>> shift2;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedRightShift_WithCompoundAssignment_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedRightShiftCompoundTest
    {
        [EnforcePure]
        public int UnsignedRightShiftCompoundAssignment(int value, int shift)
        {
            int result = value;
            result >>>= shift;
            return result;
        }

        [EnforcePure]
        public (int, int) MultipleUnsignedRightShiftOperations(int value1, int value2, int shift)
        {
            // Multiple operations with >>> operator
            value1 >>>= shift;
            value2 = value2 >>> shift;
            return (value1, value2);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedRightShift_WithExpression_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedRightShiftExpressionTest
    {
        [EnforcePure]
        public int UnsignedRightShiftInExpression(int value, int shift)
        {
            // Using >>> operator in a larger expression
            return (value + 10) >>> shift;
        }

        [EnforcePure]
        public int ComplexExpressionWithUnsignedRightShift(int value, int shift1, int shift2)
        {
            // Complex expression with >>> operator
            return (value << 2) >>> shift1 + (value >>> shift2) * 2;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedRightShift_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



namespace TestNamespace
{
    public class UnsignedRightShiftImpureTest
    {
        [EnforcePure]
        public void UnsignedRightShiftWithSideEffect(int value, int shift)
        {
            // Impure operation that happens to use >>> operator
            int result = value >>> shift;
            File.WriteAllText(""result.txt"", result.ToString());
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(17, 13, 17, 63)
                .WithArguments("UnsignedRightShiftWithSideEffect");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task UnsignedRightShift_ConstantExpression_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public class UnsignedRightShiftConstantTest
    {
        [EnforcePure]
        public int UnsignedRightShiftWithConstants()
        {
            // Using >>> operator with constant values
            const int value = -8;
            const int shift = 2;
            return value >>> shift;
        }

        [EnforcePure]
        public int UnsignedRightShiftWithLiterals()
        {
            // Using >>> operator with literals directly
            return -8 >>> 2;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


