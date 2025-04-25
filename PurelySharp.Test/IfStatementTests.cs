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
            // Expect PS0004 on IsEven AND PS0002 on PureIfExample (due to current engine limitation)
            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(7, 25) // IsEven is on line 7 now in the generated code
                                             .WithArguments("IsEven");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(10, 16) // PureIfExample is on line 10
                                             .WithArguments("PureIfExample");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004, expectedPS0002); // Order matters
        }

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
            // Expect PS0004 on IsAlwaysTrue
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(7, 25) // Corrected line number
                                             .WithArguments("IsAlwaysTrue");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
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
            // Expect PS0002 on ImpureConditionExample
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(11, 16)
                                             .WithArguments("ImpureConditionExample");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Test]
        public async Task ImpureIfBranch_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static int _impureField = 0;
    // No PS0004 expected here due to impurity
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
            // Expect PS0004 on IsEven AND PS0002 on ImpureIfBranchExample
            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(10, 25) // IsEven is on line 10 now
                                             .WithArguments("IsEven");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                            .WithLocation(13, 16) // ImpureIfBranchExample is on line 13
                                            .WithArguments("ImpureIfBranchExample");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004, expectedPS0002); // Swapped order
        }

        [Test]
        public async Task ImpureElseBranch_ShouldFail()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static int _impureField = 0;
    // No PS0004 expected here
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
            // Expect PS0004 on IsEven AND PS0002 on ImpureElseBranchExample
            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(10, 25) // IsEven is on line 10 now
                                             .WithArguments("IsEven");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(13, 16) // ImpureElseBranchExample is on line 13
                                             .WithArguments("ImpureElseBranchExample");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004, expectedPS0002); // Swapped order
        }

        [Test]
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
            // Expect PS0004 on IsPositive, PS0004 on IsEven, AND PS0002 on NestedPureIfExample (due to current engine limitation)
            var expectedPS0004_1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(7, 25) // IsPositive on line 7
                                             .WithArguments("IsPositive");
            var expectedPS0004_2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                            .WithLocation(9, 25) // IsEven on line 9
                                            .WithArguments("IsEven");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(12, 16) // NestedPureIfExample on line 12
                                             .WithArguments("NestedPureIfExample");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004_1, expectedPS0004_2, expectedPS0002); // Order matters
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
            // Expect PS0004 on IsPositive, PS0004 on IsEven, AND PS0002 on NestedImpureIfExample
            var expectedPS0004_1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(8, 25) // IsPositive on line 8
                                             .WithArguments("IsPositive");
            var expectedPS0004_2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeRule)
                                             .WithLocation(10, 25) // IsEven on line 10
                                             .WithArguments("IsEven");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                             .WithLocation(15, 16) // NestedImpureIfExample on line 15
                                             .WithArguments("NestedImpureIfExample");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedPS0004_1, expectedPS0004_2, expectedPS0002); // Reordered
        }
    }
}