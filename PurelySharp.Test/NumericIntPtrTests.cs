using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // Added for [EnforcePure]
using System; // Added for nint/nuint etc.
using System.IO; // Added for File access example

namespace PurelySharp.Test
{
    [TestFixture]
    public class NumericIntPtrTests
    {
        // Define the minimal attribute source once
        private const string AttributeSource = @"
[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : System.Attribute { }
";

        // Helper method to combine attribute source with test code
        private string CreateTestWithAttribute(string testCode)
        {
            // Basic structure assuming testCode is within a namespace and potentially a class
            return $@"
using System;
using PurelySharp.Attributes;
{AttributeSource}

namespace TestNamespace
{{
{testCode}
}}";
        }


        [Test]
        public async Task NativeInt_PureMethod_NoDiagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes; // Assuming AttributeSource is prepended elsewhere or implicitly

    public class NativeIntArithmetic
    {
        // Should trigger PS0004 as it appears pure but lacks attribute
        public nint {|PS0004:Add|}(nint a, nint b)
        {
            return a + b;
        }

        // Should trigger PS0004
        public nint {|PS0004:Multiply|}(nint a, nint b)
        {
            return a * b;
        }
    }";
            var test = CreateTestWithAttribute(testCode);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UnsignedNativeInt_PureMethod_NoDiagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes;

    public class UnsignedNativeIntArithmetic
    {
        // Should trigger PS0004
        public nuint {|PS0004:Add|}(nuint a, nuint b)
        {
            return a + b;
        }

        // Should trigger PS0004
        public nuint {|PS0004:Multiply|}(nuint a, nuint b)
        {
            return a * b;
        }
    }";
            var test = CreateTestWithAttribute(testCode);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithConversions_PureMethod_NoDiagnostic()
        {
            var testCode = @"
    public class NativeIntConversions
    {
        // Casts were handled, expect no diagnostic for the method itself if pure
        public int ConvertToInt(nint value)
        {
             return (int)value;
        }

        public nint ConvertToNInt(int value)
        {
            return (nint)value;
        }

         public uint ConvertToUInt(nuint value)
        {
             return (uint)value;
        }

        public nuint ConvertToNUInt(uint value)
        {
            return (nuint)value;
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(CreateTestWithAttribute(testCode));
        }

        [Test]
        public async Task NativeIntWithComparisons_PureMethod_NoDiagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes;

    public class NativeIntComparer
    {
        // PS0004 expected
        public bool {|PS0004:IsGreaterThan|}(nint a, nint b)
        {
            return a > b;
        }

        // PS0004 expected
        public bool {|PS0004:IsLessThan|}(nint a, nint b)
        {
            return a < b;
        }

        // PS0004 expected
        public bool {|PS0004:AreEqual|}(nint a, nint b)
        {
            return a == b;
        }

        // PS0004 expected (overload)
        public bool {|PS0004:IsGreaterThan|}(nuint a, nuint b)
        {
            return a > b;
        }
    }";
            var test = CreateTestWithAttribute(testCode);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithConstants_PureMethod_NoDiagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes;

    public class NativeIntConstants
    {
        // PS0004 expected
        public nint {|PS0004:GetLargePositiveValue|}()
        {
            return (nint)1000000000;
        }

        // PS0004 expected
        public nint {|PS0004:GetNegativeValue|}()
        {
            return (nint)(-1000000000);
        }

        // PS0004 expected
        public nuint {|PS0004:GetLargeUnsignedValue|}()
        {
            return (nuint)4000000000;
        }

        // PS0004 expected
        public nuint {|PS0004:GetZeroValue|}()
        {
            return (nuint)0;
        }
    }";
            var test = CreateTestWithAttribute(testCode);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithBitwiseOperations_PureMethod_NoDiagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes;

    public class NativeIntBitwise
    {
        // PS0004 expected
        public nint {|PS0004:BitwiseAnd|}(nint a, nint b)
        {
            return a & b;
        }

        // PS0004 expected
        public nint {|PS0004:BitwiseOr|}(nint a, nint b)
        {
            return a | b;
        }

        // PS0004 expected
        public nint {|PS0004:BitwiseXor|}(nint a, nint b)
        {
            return a ^ b;
        }

        // PS0004 expected
        public nint {|PS0004:BitwiseNot|}(nint a)
        {
            return ~a;
        }

        // PS0004 expected (overload)
        public nuint {|PS0004:BitwiseAnd|}(nuint a, nuint b)
        {
            return a & b;
        }
    }";
            var test = CreateTestWithAttribute(testCode);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NativeIntWithLogging_ImpureMethod_Diagnostic()
        {
            var testCode = @"
using System.IO; // Needed for File
using PurelySharp.Attributes;

namespace TestNamespace 
{
    public class NativeIntWithLogging
    {
        // The method identifier is where PS0002 should be reported
        [EnforcePure]
        public nint {|PS0002:AddAndLog|}(nint a, nint b) 
        {
            nint result = a + b; 
            File.AppendAllText(""log.txt"", $""{result}""); // Impure File IO causes method to fail verification
            return result;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task NativeIntImpureMethod_Diagnostic()
        {
            // This test passed previously after fixing compound assignment.
            // No changes needed here.
            var testCode = @"
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class NativeIntImpureOperations
    {
        private static nint _state = 0;

        [EnforcePure]
        public nint {|PS0002:ImpureAdd|}(nint a)
        {
            // The compound assignment modifies state, making method impure.
            _state += a; 
            return _state;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}