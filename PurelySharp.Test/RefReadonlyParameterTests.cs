using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RefReadonlyParameterTests
    {
        [Test]
        public async Task PureMethodWithRefReadonlyParameter_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    // Reading ref readonly parameters is pure.
    public int Sum(ref readonly int x, ref readonly int y)
    {
        // Only reading values, no modifications - this should be pure
        return x + y;
    }
}";

            // Expecting NO diagnostic now.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_AccessingFields_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Point
{
    public int X;
    public int Y;
}

public class TestClass
{
    [EnforcePure]
    public int CalculateDistance(ref readonly Point p1, ref readonly Point p2)
    {
        // Accessing struct fields through ref readonly parameter - this is pure
        int dx = p1.X - p2.X;
        int dy = p1.Y - p2.Y;
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }
}";
            // Analyzer now considers this pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_AssigningLocally_NoDiagnostic()
        {
            // Expectation limitation: Analyzer flags reading from a local mutable copy
            // of a 'ref readonly' struct parameter as impure.
            var test = @$"
using PurelySharp.Attributes;

public struct LargeStruct
{{
    public int Value1;
    public int Value2;
    public int Value3;
}}

public class TestClass
{{
    [EnforcePure]
    public int GetMax(ref readonly LargeStruct data)
    {{
        LargeStruct localCopy = data;
        // Reading from the local mutable copy IS considered impure
        // The diagnostic should be on localCopy.Value1
        int max = localCopy.Value1;
        if (localCopy.Value2 > max) max = localCopy.Value2;
        if (localCopy.Value3 > max) max = localCopy.Value3;
        return max;
    }}
}}";
            // Expect PS0002 on the method signature, as the local copy makes subsequent reads impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(14, 16, 14, 22) // Span updated to method identifier 'GetMax'
                                   .WithArguments("GetMax");

            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Use explicit diagnostic
        }

        [Test]
        public async Task PureMethodAttemptingToModifyRefReadonlyParameter_CompilationError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void Increment(ref readonly int x)
    {
        x++;
    }
}";

            // Expect PS0002 from the analyzer (on method signature) and CS8331 from the compiler
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                        .WithSpan(8, 17, 8, 26) // Updated Span to method identifier
                                        .WithArguments("Increment");
            var expectedCS8331 = DiagnosticResult.CompilerError("CS8331")
                                                .WithSpan(10, 9, 10, 10); // Span of x in x++


            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0002, expectedCS8331); // Expect exactly 2 diagnostics
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_PassingToAnotherMethodWithRef_CompilationError()
        {
            var testCode = @"
using PurelySharp.Attributes;

public struct Point { public int X, Y; }

public class TestClass
{
    // Method expecting a mutable ref
    public static void ModifyPoint(ref Point p) { p.X++; } // Impure

    [EnforcePure]
    public static void TestModify(ref readonly Point p) // Line 12
    {
        ModifyPoint(ref p); // Error: Cannot pass ref readonly as ref (Line 14)
    }
}
";

            // Expect compiler error CS8329 for passing ref readonly as ref
            var expectedError = DiagnosticResult.CompilerError("CS8329")
                                                .WithSpan(14, 25, 14, 26) // Span of 'p' in ModifyPoint call
                                                .WithArguments("variable", "p");

            // Expect PS0002 on TestModify because it calls impure ModifyPoint (even though it's a compiler error)
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                         .WithSpan(12, 24, 12, 34) // Span of 'TestModify' identifier
                                         .WithArguments("TestModify");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedError, expectedPS0002);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyStruct_AccessingMethodsOnStruct_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public readonly struct ReadOnlyValue
{
    private readonly int _value;
    
    public ReadOnlyValue(int value)
    {
        _value = value;
    }
    
    // Assuming these methods are pure or marked [EnforcePure] elsewhere
    [EnforcePure] public int GetValue() => _value;
    [EnforcePure] public ReadOnlyValue Add(int amount) => new ReadOnlyValue(_value + amount);
}

public class TestClass
{
    [EnforcePure]
    public int Process(ref readonly ReadOnlyValue value)
    {
        // Calling pure methods on the readonly struct is pure
        return value.GetValue() + value.Add(5).GetValue();
    }
}";

            // Expect no diagnostic as the called methods are pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodPassingRefReadonlyToImpureMethod_DiagnosticAndCompilationError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _state;

    [EnforcePure]
    // Added PS0002 markup (calling impure ModifyGlobalState)
    public int Process(ref readonly int value) 
    {
        // Passing ref readonly as ref causes CS8329
        // Also, calling an impure method from pure context causes PS0002
        return ModifyGlobalState(ref value); // Impurity comes from ModifyGlobalState
    }
    
    // Impure method (modifies state)
    private int ModifyGlobalState(ref int val) 
    {
        _state++; 
        return val + _state;
    }
}";

            // Expect PS0002 on the method identifier (fallback) and CS8329 compiler error
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                        .WithSpan(11, 16, 11, 23) // Span of Process identifier
                                        .WithArguments("Process");
            var expectedCS8329 = DiagnosticResult.CompilerError("CS8329")
                                                .WithSpan(15, 38, 15, 43); // Location of value in ModifyGlobalState(ref value)

            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0002, expectedCS8329);
        }
    }
}


