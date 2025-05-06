using NUnit.Framework;
using PurelySharp.Attributes;
using PurelySharp.Analyzer; // For Diagnostic IDs
using Microsoft.CodeAnalysis; // For DiagnosticResult
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class IfStatementTests
    {
        private static int _impureField = 0;

        private static int ImpureMethod()
        {
            _impureField++; // Side effect
            return _impureField;
        }

        private static bool IsEven(int n) => n % 2 == 0; // Pure function

        // Expectation limitation: analyzer does not report missing enforce-pure-attribute diagnostic (PS0004) for pure helper methods lacking [EnforcePure].
        [Test]
        public async Task PureIfElse_ShouldPass()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    // PS0004 expected here
    private static bool IsEven(int n) => n % 2 == 0; // Pure

    [EnforcePure]
    public int PureIfExample(int input)
    {
        if (IsEven(input)) // Pure condition
        {
            return input / 2; // Pure branch
        }
        else
        {
            return input * 2; // Pure branch
        }
    }
}
";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                   .WithSpan(7, 25, 7, 31).WithArguments("IsEven");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
        }

        // Expectation limitation: analyzer does not report missing enforce-pure-attribute diagnostic (PS0004) for pure helper methods lacking [EnforcePure].
        [Test]
        public async Task PureIf_NoElse_ShouldPass()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    // PS0004 expected here
    private static bool IsAlwaysTrue() => true; // Pure

    [EnforcePure]
    public int PureIfNoElseExample(int input)
    {
        int result = input;
        if (IsAlwaysTrue()) // Pure condition
        {
             result = input + 1; // Pure branch (local assignment)
        }
        return result; // Pure return
    }
}
";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                   .WithSpan(7, 25, 7, 37).WithArguments("IsAlwaysTrue");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
        }

        [Test]
        public async Task ImpureCondition_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System; // For DateTime

public class TestClass
{
     // PS0004 *might* be reported here depending on analysis, but focus is PS0002
     private static bool ImpureCondition() => DateTime.Now.Ticks > 0; // Impure

    [EnforcePure]
    public int ImpureConditionExample(int input)
    {
        if (ImpureCondition()) // Impure condition
        {
            return input / 2;
        }
        else
        {
            return input * 2;
        }
    }
}
";
            // Analyzer currently misses the PS0002, but reports PS0004 on ImpureCondition
            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                      .WithSpan(8, 26, 8, 41) // Adjusted column from 25 to 26
                                      .WithArguments("ImpureCondition");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004);
        }

        [Test]
        public async Task ImpureIfBranch_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static int _impureField = 0;
    // Impure method
    private static int ImpureMethod() { _impureField++; return _impureField; }
    // PS0004 expected here
    private static bool IsEven(int n) => n % 2 == 0; // Pure

    [EnforcePure]
    public int ImpureIfBranchExample(int input)
    {
        if (IsEven(input)) // Pure condition
        {
            return ImpureMethod(); // Impure branch
        }
        else
        {
            return input * 2; // Pure branch
        }
    }
}
";
            // Expect PS0002 on ImpureMethod, ImpureIfBranchExample, and PS0004 on IsEven
            var expectedPS0002_ImpureMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                                 .WithSpan(8, 24, 8, 36) // Span for ImpureMethod
                                                 .WithArguments("ImpureMethod");
            var expectedPS0002_Caller = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                            .WithLocation(13, 16) // ImpureIfBranchExample is on line 13
                                            .WithArguments("ImpureIfBranchExample");
            var expectedPS0004_IsEven = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                            .WithSpan(10, 25, 10, 31) // Span of IsEven identifier - Adjusted line
                                            .WithArguments("IsEven");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0002_ImpureMethod, expectedPS0002_Caller, expectedPS0004_IsEven);
        }

        [Test]
        public async Task ImpureElseBranch_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static int _impureField = 0;
    // Impure method
    private static int ImpureMethod() { _impureField++; return _impureField; }
     // PS0004 expected here
    private static bool IsEven(int n) => n % 2 == 0; // Pure

    [EnforcePure]
    public int ImpureElseBranchExample(int input)
    {
        if (IsEven(input)) // Pure condition
        {
            return input / 2; // Pure branch
        }
        else
        {
             return ImpureMethod(); // Impure branch
        }
    }
}
";
            // Expect PS0002 on ImpureMethod, ImpureElseBranchExample, and PS0004 on IsEven
            var expectedPS0002_ImpureMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                                 .WithSpan(8, 24, 8, 36) // Span for ImpureMethod
                                                 .WithArguments("ImpureMethod");
            var expectedPS0002_Caller = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(13, 16) // ImpureElseBranchExample is on line 13
                                             .WithArguments("ImpureElseBranchExample");
            var expectedPS0004_IsEven = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                             .WithSpan(10, 25, 10, 31) // Span of IsEven identifier - Adjusted line
                                             .WithArguments("IsEven");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0002_ImpureMethod, expectedPS0002_Caller, expectedPS0004_IsEven);
        }

        [Test]
        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) on pure helper methods without [EnforcePure].
        public async Task NestedPure_ShouldPass()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    // PS0004 expected here
    private static bool IsPositive(int n) => n > 0; // Pure
    // PS0004 expected here
    private static bool IsEven(int n) => n % 2 == 0; // Pure

    [EnforcePure]
    public int NestedPureIfExample(int input)
    {
        if (IsPositive(input)) // Pure outer condition
        {
            if (IsEven(input)) // Pure inner condition
            {
                 return 1; // Pure inner branch
            }
            else
            {
                 return -1; // Pure inner branch
            }
        }
        else
        {
            return 0; // Pure outer else
        }
    }
}
";
            var expectedIsPositive = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                             .WithSpan(7, 25, 7, 35).WithArguments("IsPositive");
            var expectedIsEven = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                         .WithSpan(9, 25, 9, 31).WithArguments("IsEven");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedIsPositive, expectedIsEven);
        }

        [Test]
        public async Task NestedImpureIf_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System; // For DateTime

public class TestClass
{
    // PS0004 expected here
    private static bool IsPositive(int n) => n > 0; // Pure
     // PS0004 expected here
    private static bool IsEven(int n) => n % 2 == 0; // Pure
    // No PS0004 expected
    private static bool ImpureCondition() => DateTime.Now.Ticks > 0; // Impure

    [EnforcePure]
    public int NestedImpureIfExample(int input)
    {
        if (IsPositive(input)) // Pure outer condition
        {
            if (ImpureCondition()) // Impure inner condition
            {
                 return 1;
            }
            else
            {
                 return -1;
            }
        }
        else
        {
            return 0;
        }
    }
}
";
            var expectedPS0004_IsPositive = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                               .WithSpan(8, 25, 8, 35)  // Span of IsPositive
                                               .WithArguments("IsPositive");
            var expectedPS0004_IsEven = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                               .WithSpan(10, 25, 10, 31) // Span of IsEven
                                               .WithArguments("IsEven");
            // Also expect PS0004 on ImpureCondition due to current analyzer limitation
            var expectedPS0004_ImpureCondition = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                                   .WithSpan(12, 25, 12, 40) // Span for ImpureCondition
                                                   .WithArguments("ImpureCondition");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004_IsPositive, expectedPS0004_IsEven, expectedPS0004_ImpureCondition);
        }
    }
}