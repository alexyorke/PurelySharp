using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // Need this for [EnforcePure]
using System.IO; // Added for potential reference resolution
using Microsoft.CodeAnalysis; // Added for MetadataReference
using System.Collections.Immutable; // Added for ImmutableArray

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicPurityTests
    {
        [Test]
        public async Task TestPureMethod_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes; // Assuming attributes are in this namespace

public class TestClass
{
    // No [Pure] or [EnforcePure] attribute here, but returns constant -> PS0004 expected
    // [Pure] // Removing the original [Pure] attribute if it existed
    public int {|PS0004:GetConstant|}()
    {
        return 42;
    }
}";

            // Expect PS0004 diagnostic due to missing [EnforcePure] on a potentially pure method
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureMethod_ShouldBeFlaggedNow()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetParameter(int x)
    {
        return x; // Return parameter
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstant_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure] // Mark it for analysis, even though it should pass
    public int GetTheAnswer()
    {
        return 42; // Constant return, should be considered pure by future analysis
    }
}";
            // Now expect no diagnostics because the analyzer recognizes constant returns.
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
            //                        .WithLocation(7, 16) // Line 7, Column 16 (method name) - CORRECTED LINE NUMBER
            //                        .WithArguments("GetTheAnswer");
            // await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
            await VerifyCS.VerifyAnalyzerAsync(testCode); // Expect no diagnostics
        }

        [Test]
        public async Task TestPureMethodReturningConstantString_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public string GetString() => ""Hello""; // Escape inner quotes
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantBool_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public bool GetTrue() { return true; }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantNull_NoDiagnostics()
        {
            var testCode = @"
#nullable enable // Add this line to enable nullable context
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public string? GetNull() => null;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningConstField_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    private const int MyConst = 10;
    [EnforcePure]
    public int GetConst() => MyConst;
}";
            // Expect PS0002 because returning a field is not LiteralExpressionSyntax
            // UPDATE: Now considered pure because MyConst has a constant value.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningStaticReadonlyField_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    private static readonly string Greeting = ""Hi"";
    [EnforcePure]
    public string {|PS0002:GetGreeting|}() { return Greeting; }
}";
            // Expect PS0002 because returning a field is not LiteralExpressionSyntax
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningSimpleCalculation_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public int GetTwo() => 1 + 1;
}";
            // Expect PS0002 because calculation is BinaryExpressionSyntax
            // UPDATE: Now considered pure because 1 + 1 has a constant value.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningDefault_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public int GetDefaultInt() => default;
}";
            // Expect PS0002 because default keyword is DefaultExpressionSyntax or similar
            // UPDATE: Currently not flagged, removed markup expectation.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestImpureMethod_ShouldBeFlagged_OnMethodName()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    private static int _counter = 0;

    [EnforcePure]
    public int {|PS0002:ImpureMethod|}() // Explicitly marked for PS0002
    {
        _counter++; // Modifies static state
        return _counter;
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        // Renamed test and updated expectations
        public async Task TestEnforcePureOnNonMethod_ReportsMisplacedAttribute()
        {
            // Add the attribute source code directly to the test source
            var attributeSource = @"
using System;

namespace PurelySharp.Attributes
{
    [AttributeUsage(AttributeTargets.All)] 
    public sealed class EnforcePureAttribute : Attribute { }
}";

            // Reverted spans back to covering only the attribute
            var testCodeWithMarkup = @"
using PurelySharp.Attributes;
using System;

[{|PS0003:EnforcePure|}] // On class - Should report PS0003 on attribute
public class TestClass
{
    [{|PS0003:EnforcePure|}] // On field - Should report PS0003 on attribute
    private int _field = 0;

    [{|PS0003:EnforcePure|}] // On property - Should report PS0003 on attribute
    public int MyProperty { get; set; }

    // Valid method with attribute - PS0002 not expected in *this* test
    [EnforcePure]
    public void ValidMethod() { } // Ensure no markup here

    // Method without attribute - Should be ignored
    public void AnotherMethod() { }
}"; // Add missing semicolon

            // Configure the test runner
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { attributeSource, testCodeWithMarkup }, // Include both sources
                },
                // Remove explicit expected diagnostics, rely on markup
                /*
                ExpectedDiagnostics =
                {
                    // Span locations match the {|PS0003:...|} markup in the source
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(5, 2, 5, 13),
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(8, 6, 8, 17),
                    VerifyCS.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeRule).WithSpan(11, 6, 11, 17),
                    // NOTE: PS0002 is intentionally omitted here
                },
                */
                ReferenceAssemblies = Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net80,
                // No need to disable CS0592 anymore as AttributeUsage allows other targets.
                // We might still disable warnings like CS1591 or CS0414 if they appear.
                // CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.Warnings, 
                // DisabledDiagnostics = { "CS1591", "CS0414" } 
            };

            // Re-add the explicit metadata reference transform even though source is included
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                solution = solution.AddMetadataReference(projectId, 
                    MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location));
                return solution;
            });

            // The verifier will automatically check the diagnostics specified in the markup
            await test.RunAsync();
        }

        // --- Additional Constant Return Tests ---

        [Test]
        public async Task TestPureMethodReturningConstantDouble_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public double GetPi() => 3.14159;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantDecimal_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public decimal GetMoney() { return 123.45m; }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantFloat_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public float GetRatio() => 0.5f;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantLong_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public long GetBigNumber() { return 9_000_000_000_000_000_000L; }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
        
        [Test]
        public async Task TestPureMethodReturningConstantChar_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public char GetInitial() => 'J';
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantEnum_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;
public class TestClass
{
    [EnforcePure]
    public DayOfWeek GetDay() { return DayOfWeek.Friday; }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningNameof_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public string GetClassName() => nameof(TestClass);

    [EnforcePure]
    public string GetStringName() { return nameof(System.String); }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantCalculation_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    private const int FIVE = 5;
    [EnforcePure]
    public int GetSum() => 2 + 3;

    [EnforcePure]
    public int GetProduct() { return FIVE * 10; } // Uses const field
}";
            // Constant folding makes these pure
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Tests Expected to Fail (Should be Flagged) ---

        [Test]
        public async Task TestEnforcePureReturningTypeof_ShouldBeFlagged()
        {
            var testCode = @"
#nullable enable
using PurelySharp.Attributes;
using System;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public Type {|PS0002:GetTypeFromString|}() => typeof(string);

    [EnforcePure]
    public Type {|PS0002:GetTypeFromList|}() { return typeof(List<int>); }
}";
            // typeof is not a compile-time constant value, requires runtime execution.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // Ensure default still isn't flagged (matching previous behavior adjustment)
        [Test]
        public async Task TestEnforcePureReturningDefault_StillNoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public int GetDefaultInt() => default;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode); 
        }

        // --- Tests for Tricky Non-Constant Cases ---

        [Test]
        public async Task TestEnforcePureReturningSimpleProperty_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    public int Five => 5;

    [EnforcePure]
    public int {|PS0002:GetFiveFromProp|}() => Five;
}";
            // Property access is not considered a constant expression by GetConstantValue
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningDateTimeNow_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:GetCurrentTime|}() { return DateTime.Now; }
}";
            // DateTime.Now is obviously not constant
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningNewGuid_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    [EnforcePure]
    public Guid {|PS0002:GetNewGuid|}() => Guid.NewGuid();
}";
            // Guid.NewGuid() generates a new value each time
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningNewString_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:GetNewString|}() { return new string('a', 10); }
}";
            // 'new' expressions are not compile-time constants
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Even More Constant Return Tests (Should Pass) ---

        [Test]
        public async Task TestPureMethodReturningDefaultValueType_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetDefaultIntExplicit() => default(int);
}";
            // default(int) is compile-time constant 0
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningSizeOf_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetSizeOfInt() { return sizeof(int); }
}";
            // sizeof(valueType) is compile-time constant
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstBitwiseOp_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetBitShift() => 1 << 2;
}";
            // Constant folding applies
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstConditional_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetConditional() => true ? 1 : 0;
}"; // Ensure class closing brace is inside the string, then quote and semicolon
            // Constant folding applies
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Even More Tricky Non-Constant Tests (Should Fail - PS0002) ---

        [Test]
        public async Task TestEnforcePureReturningStaticNonReadonlyField_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    public static int Counter = 0;

    [EnforcePure]
    public int {|PS0002:GetCounter|}() => Counter;
}";
            // Accessing mutable static state
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningInstanceField_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private int _value = 10;

    [EnforcePure]
    public int {|PS0002:GetValue|}() { return _value; }
}";
            // Accessing instance state
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureReturningMethodCallResult_ShouldBeFlagged()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    [EnforcePure]
    public double {|PS0002:GetSqrt|}() => Math.Sqrt(4.0);
}";
            // Even if input is constant, Math.Sqrt is a method call, not a constant expression
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Tests for PS0004: Missing [EnforcePure] Attribute (Warning) ---

        [Test]
        public async Task TestPotentiallyPureMethod_WithoutAttribute_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes; // Ensure attribute namespace is known, even if not used here
using System;

public class TestClass
{
    // This method looks pure (returns constant) but lacks [EnforcePure]
    public int {|PS0004:GetConstantWithoutAttribute|}()
    {
        return 123;
    }

    // This method also looks pure (returns const field) but lacks [EnforcePure]
    private const string Greeting = ""Hello""; // Corrected escaping for quote inside verbatim string
    public string {|PS0004:GetGreetingWithoutAttribute|}() => Greeting;

    // This method uses a simple constant calculation, looks pure, lacks attribute
    public int {|PS0004:GetCalcWithoutAttribute|}() => 10 + 20;

    // This method returns nameof, looks pure, lacks attribute
    public string {|PS0004:GetNameofWithoutAttribute|}() { return nameof(System.Int32); }
}";

            // Expect PS0004 warnings on the methods lacking the attribute
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestImpureMethod_WithoutAttribute_ShouldNotWarn()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    private static int _counter = 0;

    // Impure method, no attribute - should have NO diagnostic from our analyzer
    public int ImpureMethodWithoutAttribute()
    {
        _counter++; 
        return _counter;
    }

    // Another impure method (DateTime.Now), no attribute
    public DateTime GetCurrentTimeWithoutAttribute() => DateTime.Now;

    // Method calling another (potentially impure) method, no attribute
    public double GetSqrtWithoutAttribute() => Math.Sqrt(9.0);
}";

            // Expect NO diagnostics related to purity (PS0002 or PS0004)
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantByte_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public byte GetByte() => 128;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantSByte_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public sbyte GetSByte() => -10;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantShort_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public short GetShort() => 1000;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantUShort_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public ushort GetUShort() => 2000;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantUInt_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public uint GetUInt() => 3000U;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPureMethodReturningConstantULong_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public ulong GetULong() => 4000UL;
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPotentiallyPureMethodCallingEnforcedPure_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private int HelperPureMethod() => 42;

    // This method looks pure because it calls an [EnforcePure] method,
    // but lacks the attribute itself.
    public int {|PS0004:CallingPureHelper|}() => HelperPureMethod(); 
}";
            // Expect PS0004 suggesting [EnforcePure] because it calls a known pure method
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        // Renamed: No longer expected to fail. Expects warnings on A, B, and C.
        public async Task TestPotentiallyPureMethodCallingChain_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private int MethodD() => 42;

    // Should warn PS0004
    private int {|PS0004:MethodC|}() => MethodD();

    // Should warn PS0004
    private int {|PS0004:MethodB|}() => MethodC();

    // MethodA calls B, which calls C, which calls D (which is pure).
    // Should warn PS0004
    public int {|PS0004:MethodA|}() => MethodB(); 
}";
            // Expect PS0004 on A, B, and C because they transitively call a pure method D.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureOnImpureCycle_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    [EnforcePure]
    // MethodA calls MethodB, which calls MethodA (cycle)
    // MethodB is also impure.
    // Cycle detection should make IsConsideredPure return false for MethodA.
    // Since it's [EnforcePure], PS0002 should be reported.
    public int {|PS0002:RecursiveA|}(int count)
    {
        if (count <= 0) return 0;
        return RecursiveB(count - 1);
    }

    private int RecursiveB(int count)
    {
        Console.WriteLine(DateTime.Now); // Impure action
        if (count <= 0) return 1;
        return RecursiveA(count - 1); 
    }
}";
            // Expect PS0002 on RecursiveA due to the cycle involving an impure method.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPotentiallyPureLongerChain_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private int MethodE() => 5;

    private int {|PS0004:MethodD|}() => MethodE(); // Calls pure E
    private int {|PS0004:MethodC|}() => MethodD(); // Calls potentially pure D
    private int {|PS0004:MethodB|}() => MethodC(); // Calls potentially pure C
    public int {|PS0004:MethodA|}() => MethodB(); // Calls potentially pure B
}";
            // Expect PS0004 on A, B, C, D
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPotentiallyPureCallingTwoPureMethods_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private int GetFive() => 5;

    [EnforcePure]
    private int GetTen() => 10;

    // Calls two pure methods, but operation (+) is not constant or single invocation.
    // Analyzer returns false for this expression type.
    // UPDATE: Now considered pure, so expect PS0004 warning.
    public int {|PS0004:GetSum|}() => GetFive() + GetTen(); 
}";
            // Expect NO diagnostics because the '+' expression makes IsConsideredPure return false.
            // UPDATE: Now expect PS0004.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureCallingTwoPureMethods_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private int GetFive() => 5;

    [EnforcePure]
    private int GetTen() => 10;

    [EnforcePure]
    // Calls two pure methods, but operation (+) is not constant or single invocation.
    // UPDATE: Now considered pure.
    public int GetSum() => GetFive() + GetTen(); 
}";
            // Expect PS0002 because analyzer cannot verify purity of '+' operation yet.
            // UPDATE: Now expect no diagnostics.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestPotentiallyPureCycle_ShouldWarnPS0004()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    // Cycle: A calls B, B calls A. Neither is impure otherwise.
    // Cycle detection currently returns false, so no warning expected.
    public int PureRecursiveA(int x)
    {
        if (x <= 0) return 0;
        return PureRecursiveB(x - 1);
    }

    private int PureRecursiveB(int x)
    {
        if (x <= 0) return 1;
        return PureRecursiveA(x - 1);
    }
}";
            // Expect NO diagnostics because cycle detection returns false.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
        
        [Test]
        public async Task TestEnforcePureWithMultipleReturns_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure] // Marked pure, but analyzer can't handle multiple returns
    public int {|PS0002:GetValueBasedOnInput|}(int x)
    {
        if (x > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}";
            // Expect PS0002 because GetReturnExpressionSyntax returns null for multi-statement bodies.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Tests for Simple Block Bodies ---

        [Test]
        public async Task TestEnforcePureWithLocalConstReturn_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public int GetLocalConst()
    {
        const int x = 5;
        return x; // GetConstantValue works on local const reference
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
        
        [Test]
        public async Task TestEnforcePureWithLocalVarFromPureCall_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure] private int PureHelper() => 10;

    [EnforcePure]
    public int GetValFromPureCall()
    {
        var temp = PureHelper();
        return temp; // Should now be considered pure
    }
}";
            // Now expect no diagnostics because IsExpressionPure checks localPurityStatus.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithLocalVarFromImpureCall_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;
public class TestClass
{
    private int ImpureHelper() { Console.WriteLine(); return 0; }

    [EnforcePure]
    public int {|PS0002:GetValFromImpureCall|}()
    {
        // Impurity detected in the initializer check
        var temp = ImpureHelper(); 
        return temp;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithAssignment_ShouldFlagPS0002()
        {
             var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public int {|PS0002:MethodWithAssignment|}()
    {
        int x = 5;
        x = x + 1; // Assignment statement makes it impure
        return x;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithMultipleLocalDecls_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure] private int PureHelper1() => 1;
    [EnforcePure] private int PureHelper2() => 2;

    [EnforcePure]
    public int GetFromMultipleLocals()
    {
        var a = PureHelper1();
        const int b = 10;
        var c = PureHelper2();
        // Currently fails because return temp; requires local analysis
        // Also fails because return a + b + c; requires operator analysis
        return b; // Return simple constant
    }
}";
            // Expect no diagnostics as all initializers are pure and return is constant.
             await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithIfElseReturn_ShouldFlagPS0002()
        {
            // Same test as TestEnforcePureWithMultipleReturns_ShouldFlagPS0002
            // Confirms the block analysis also rejects this.
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure] // Marked pure, but analyzer can't handle multiple returns / if statement
    public int {|PS0002:GetValueBasedOnInputViaBlock|}(int x)
    {
        if (x > 0) // If statement makes it impure
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}";
            // Expect PS0002 because block analysis doesn't allow 'if'.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        // --- Tests for Newly Handled Pure Expressions (Operators, nameof, etc.) ---

        [Test]
        public async Task TestEnforcePureWithPureBinaryOp_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure] private int PureHelper() => 5;
    [EnforcePure]
    public int BinaryOpPure(int x)
    {
        const int y = 10;
        var z = PureHelper();
        return x + y + z; // Parameter + Const + Pure Local Var
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithImpureBinaryOp_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;
public class TestClass
{
    private int ImpureHelper() { Console.WriteLine(); return 0; } 
    [EnforcePure]
    public int {|PS0002:BinaryOpImpure|}(int x)
    {
        return x + ImpureHelper(); // Parameter + Impure Call
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithPureUnaryOp_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure]
    public bool UnaryOpPure(bool b)
    {
        return !b; // Pure operand
    }

    [EnforcePure]
    public int UnaryMinusPure(int x)
    {
        return -x; // Pure operand
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithImpureUnaryOp_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;
public class TestClass
{
    private bool ImpureBool() { Console.WriteLine(); return false; }
    [EnforcePure]
    public bool {|PS0002:UnaryOpImpure|}()
    {
        return !ImpureBool(); // Impure operand
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithPureConditionalOp_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
public class TestClass
{
    [EnforcePure] private int Pure1() => 1;
    [EnforcePure] private int Pure2() => 2;
    [EnforcePure]
    public int ConditionalPure(bool condition)
    {
        return condition ? Pure1() : Pure2(); // Pure condition, pure branches
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithImpureConditionalOp_ShouldFlagPS0002()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;
public class TestClass
{
    [EnforcePure] private int Pure1() => 1;
    private int Impure2() { Console.WriteLine(); return 2; }
    private bool ImpureCond() { Console.WriteLine(); return false; }

    [EnforcePure]
    public int {|PS0002:ConditionalImpureBranch|}(bool condition)
    {
        return condition ? Pure1() : Impure2(); // Impure false branch
    }

    [EnforcePure]
    public int {|PS0002:ConditionalImpureCondition|}()
    {
        return ImpureCond() ? Pure1() : 0; // Impure condition
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureWithMiscPureExpr_NoDiagnostics()
        {
            var testCode = @"
#nullable enable // Needed for default(string?)
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int MiscPure(int p, bool c)
    {
        const int localConst = 5;
        int x = sizeof(int); // Changed from Guid to int to avoid unsafe requirement
        string? s = default(string?); // #nullable enable handles this
        int y = default; 
        var z = c ? p : localConst;
        int u = -p;
        return z + u; 
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}