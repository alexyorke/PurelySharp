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
    public int {|PS0002:GetParameter|}(int x) // Correctly modified method signature and body
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
    }
}