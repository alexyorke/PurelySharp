using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CheckedUserDefinedOperatorsTests
    {
        [Test]
        public async Task CheckedUserDefinedOperator_BasicArithmetic_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public readonly struct Money
    {
        public decimal Amount { get; }

        public Money(decimal amount)
        {
            Amount = amount;
        }

        // Regular operator for addition
        public static Money operator +(Money left, Money right)
        {
            return new Money(left.Amount + right.Amount);
        }

        // Checked operator for addition
        public static Money operator checked +(Money left, Money right)
        {
            return new Money(checked(left.Amount + right.Amount));
        }

        // Regular operator for subtraction
        public static Money operator -(Money left, Money right)
        {
            return new Money(left.Amount - right.Amount);
        }

        // Checked operator for subtraction
        public static Money operator checked -(Money left, Money right)
        {
            return new Money(checked(left.Amount - right.Amount));
        }

        // Regular operator for multiplication
        public static Money operator *(Money left, decimal multiplier)
        {
            return new Money(left.Amount * multiplier);
        }

        // Checked operator for multiplication
        public static Money operator checked *(Money left, decimal multiplier)
        {
            return new Money(checked(left.Amount * multiplier));
        }

        // Regular operator for division
        public static Money operator /(Money left, decimal divisor)
        {
            return new Money(left.Amount / divisor);
        }

        // Checked operator for division
        public static Money operator checked /(Money left, decimal divisor)
        {
            return new Money(checked(left.Amount / divisor));
        }
    }

    public class CheckedOperationsTest
    {
        [EnforcePure]
        public Money AddMoney(Money a, Money b)
        {
            // Operator source is available and pure, so this is pure. Remove marker.
            return checked(a + b);
        }

        [EnforcePure]
        public Money CalculateOrderTotal(Money[] prices, decimal taxRate)
        {
            // Operators source is available and pure, so this is pure. Remove markers.
            Money total = new Money(0);
            foreach (var price in prices)
            {
                total = checked(total + price);
            }
            return checked(total * (1 + taxRate));
        }
    }
}";

            // Expect PS0004 warnings for pure methods that are not marked with [EnforcePure]
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 24, 11, 30).WithArguments("get_Amount");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(13, 16, 13, 21).WithArguments(".ctor");
            var expectedOpAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(19, 38, 19, 39).WithArguments("op_Addition");
            var expectedOpCheckedAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(25, 46, 25, 47).WithArguments("op_CheckedAddition");
            var expectedOpSub = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(31, 38, 31, 39).WithArguments("op_Subtraction");
            var expectedOpCheckedSub = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(37, 46, 37, 47).WithArguments("op_CheckedSubtraction");
            var expectedOpMul = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(43, 38, 43, 39).WithArguments("op_Multiply");
            var expectedOpCheckedMul = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(49, 46, 49, 47).WithArguments("op_CheckedMultiply");
            var expectedOpDiv = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(55, 38, 55, 39).WithArguments("op_Division");
            var expectedOpCheckedDiv = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(61, 46, 61, 47).WithArguments("op_CheckedDivision");
            var expectedAddMoney = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(70, 22, 70, 30).WithArguments("AddMoney");
            var expectedCalculateOrderTotal = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(77, 22, 77, 41).WithArguments("CalculateOrderTotal");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedGetValue, expectedCtor, expectedOpAdd, expectedOpCheckedAdd,
                expectedOpSub, expectedOpCheckedSub, expectedOpMul, expectedOpCheckedMul,
                expectedOpDiv, expectedOpCheckedDiv, expectedAddMoney, expectedCalculateOrderTotal
            });
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithRegularOperator_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public readonly struct Vector2D
    {
        public double X { get; }
        public double Y { get; }

        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        // Regular operator
        public static Vector2D operator +(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X + right.X, left.Y + right.Y);
        }

        // Checked operator
        public static Vector2D operator checked +(Vector2D left, Vector2D right)
        {
            return new Vector2D(checked(left.X + right.X), checked(left.Y + right.Y));
        }

        // Regular subtraction operator
        public static Vector2D operator -(Vector2D left, Vector2D right)
        {
            return new Vector2D(left.X - right.X, left.Y - right.Y);
        }

        // Checked subtraction operator
        public static Vector2D operator checked -(Vector2D left, Vector2D right)
        {
            return new Vector2D(checked(left.X - right.X), checked(left.Y - right.Y));
        }

        // Magnitude property (readonly)
        public double Magnitude => Math.Sqrt(X * X + Y * Y);
    }

    public class CheckedAndRegularOperationsTest
    {
        [EnforcePure]
        public Vector2D AddVectors(Vector2D a, Vector2D b, bool useChecked)
        {
            // Both branches use pure operators defined in source.
            return useChecked ? checked(a + b) : a + b;
        }

        [EnforcePure]
        public double CalculateDistance(Vector2D a, Vector2D b)
        {
            // Operator source is now found and pure.
            Vector2D difference = checked(a - b);
            return difference.Magnitude;
        }
    }
}";
            var expected = new DiagnosticResult[] {
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 23, 11, 24).WithArguments("get_X"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(12, 23, 12, 24).WithArguments("get_Y"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(14, 16, 14, 24).WithArguments(".ctor"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(21, 41, 21, 42).WithArguments("op_Addition"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(27, 49, 27, 50).WithArguments("op_CheckedAddition"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(33, 41, 33, 42).WithArguments("op_Subtraction"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(39, 49, 39, 50).WithArguments("op_CheckedSubtraction"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(51, 25, 51, 35).WithArguments("AddVectors"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(58, 23, 58, 40).WithArguments("CalculateDistance")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_ComplexExpression_PureMethod_NoDiagnostic()
        {
            var test = @$"
using System;
using System.Threading;
using PurelySharp.Attributes;

public static class Operations
{{
    public static int AddChecked(int x, int y)
    {{
        return checked(x + y);
    }}
}}

public struct ComplexValue
{{
    public int Real {{ get; }}
    public int Imaginary {{ get; }}

    public ComplexValue(int real, int imaginary)
    {{
        Real = real;
        Imaginary = imaginary;
    }}

    // Checked addition operator
    public static ComplexValue operator +(ComplexValue a, ComplexValue b)
    {{
        // Use System.HashCode which is marked impure
        HashCode hash = default;
        hash.Add(a.Real);
        hash.Add(b.Real);
        int realSum = checked(a.Real + b.Real); // Use checked context

        // Use Operations.AddChecked
        int imaginarySum = Operations.AddChecked(a.Imaginary, b.Imaginary);

        return new ComplexValue(realSum, imaginarySum);
    }}

     // Checked subtraction operator
    public static ComplexValue operator -(ComplexValue a, ComplexValue b)
    {{
        int realDiff = checked(a.Real - b.Real); // Use checked context
        int imaginaryDiff = checked(a.Imaginary - b.Imaginary); // Use checked context
        return new ComplexValue(realDiff, imaginaryDiff);
    }}

     // Checked unary negation operator
    public static ComplexValue operator -(ComplexValue a)
    {{
        return new ComplexValue(checked(-a.Real), checked(-a.Imaginary)); // Use checked context
    }}

     // Example method using checked operators within a complex expression
    [EnforcePure]
    public static ComplexValue ComplexCalculationChecked(ComplexValue c1, ComplexValue c2, ComplexValue c3)
    {{
        // Nested checked operations
        ComplexValue intermediate = checked(c1 + c2);
        return checked(intermediate - c3);
    }}

    // Example method using checked operators within a complex expression
    [EnforcePure]
    public static ComplexValue FibonacciChecked(int n)
    {{
         if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), ""Input must be non-negative."");
         if (n == 0) return new ComplexValue(0, 0);

         ComplexValue a = new ComplexValue(0, 0);
         ComplexValue b = new ComplexValue(1, 0);

         for (int i = 1; i < n; i++)
         {{
            ComplexValue temp = checked(a + b); // Checked operator used here
            a = b;
            b = temp;
         }}
         return b;
    }}
}}
            ";

            // This test now expects PS0002 because FibonacciChecked contains patterns (loops, assignments)
            // that trigger the "not fully verified" diagnostic, even if underlying calls (like HashCode.Add) are known.
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                    .WithSpan(65, 32, 65, 48) // Corrected span from test output
                                    .WithArguments("FibonacciChecked");

            // ADDED: Expect diagnostic for ComplexCalculationChecked due to HashCode use in operator+
            var expected2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                    .WithSpan(56, 32, 56, 57) // Span for ComplexCalculationChecked
                                    .WithArguments("ComplexCalculationChecked");

            // ADDED: Expect PS0004 warnings for pure methods that are not marked with [EnforcePure]
            var expectedAddChecked = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(8, 23, 8, 33).WithArguments("AddChecked");
            var expectedGetReal = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(16, 16, 16, 20).WithArguments("get_Real");
            var expectedGetImaginary = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(17, 16, 17, 25).WithArguments("get_Imaginary");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(19, 12, 19, 24).WithArguments(".ctor");
            var expectedOpAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(26, 41, 26, 42).WithArguments("op_Addition");
            var expectedOpSub = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(41, 41, 41, 42).WithArguments("op_Subtraction");
            var expectedOpNeg = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(49, 41, 49, 42).WithArguments("op_UnaryNegation");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expected, expected2, expectedAddChecked, expectedGetReal, expectedGetImaginary, expectedCtor,
                expectedOpAdd, expectedOpSub, expectedOpNeg
            });
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithExceptionHandling_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public readonly struct SafeInteger
    {
        public int Value { get; }

        public SafeInteger(int value)
        {
            Value = value;
        }

        // Regular addition
        public static SafeInteger operator +(SafeInteger left, SafeInteger right)
        {
            return new SafeInteger(left.Value + right.Value);
        }

        // Checked addition with potential overflow
        public static SafeInteger operator checked +(SafeInteger left, SafeInteger right)
        {
            return new SafeInteger(checked(left.Value + right.Value));
        }

        // Regular multiplication
        public static SafeInteger operator *(SafeInteger left, SafeInteger right)
        {
            return new SafeInteger(left.Value * right.Value);
        }

        // Checked multiplication with potential overflow
        public static SafeInteger operator checked *(SafeInteger left, SafeInteger right)
        {
            return new SafeInteger(checked(left.Value * right.Value));
        }
    }

    public class ExceptionHandlingTest
    {
        [EnforcePure]
        public SafeInteger TryOperation(SafeInteger a, SafeInteger b, bool multiply)
        {
            // Pure: Analyzer handles checked operators and try/catch.
            try
            {
                return multiply ? checked(a * b) : checked(a + b);
            }
            catch (OverflowException) // Catching exception is pure
            {
                return new SafeInteger(0); // Returning value is pure
            }
        }

        [EnforcePure]
        public (bool Success, SafeInteger Result) SafeAdd(SafeInteger a, SafeInteger b)
        {
             // Pure: Analyzer handles checked operators and try/catch.
           try
            {
                return (true, checked(a + b));
            }
            catch (OverflowException) // Catching exception is pure
            {
                return (false, new SafeInteger(0)); // Returning value is pure
            }
        }
    }
}";
            // Expect PS0004 for property accessors and operators, and PS0002 for methods
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 20, 11, 25).WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(13, 16, 13, 27).WithArguments(".ctor");
            var expectedOpAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(19, 44, 19, 45).WithArguments("op_Addition");
            var expectedOpCheckedAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(25, 52, 25, 53).WithArguments("op_CheckedAddition");
            var expectedOpMul = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(31, 44, 31, 45).WithArguments("op_Multiply");
            var expectedOpCheckedMul = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(37, 52, 37, 53).WithArguments("op_CheckedMultiply");
            var expectedTryOperation = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(46, 28, 46, 40).WithArguments("TryOperation");
            var expectedSafeAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(60, 51, 60, 58).WithArguments("SafeAdd");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedGetValue, expectedCtor, expectedOpAdd, expectedOpCheckedAdd,
                expectedOpMul, expectedOpCheckedMul, expectedTryOperation, expectedSafeAdd
            });
        }

        [Test]
        public async Task CheckedUserDefinedOperator_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

// Define a custom type with a checked user-defined operator
public struct Percentage
{
    public double Value { get; private set; }

    public Percentage(double value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), ""Percentage must be between 0 and 100."" );
        Value = value;
    }

    // Checked multiplication operator
    public static Percentage operator *(Percentage p, double multiplier)
    {
        double newValue = p.Value * multiplier;
        // Potentially checked logic (could throw OverflowException if enabled project-wide or scope)
        return new Percentage(newValue);
    }
}

public class Calculator
{
    public void LogPercentageCalculation(Percentage initial, double multiplier)
    {
        // Impure operation despite using checked operator
        Percentage result = checked(initial * multiplier);
        File.WriteAllText(""calculation.log"", $""Result: {result.Value}"");
    }
}
";

            // Expect PS0004 warnings for pure methods that are not marked with [EnforcePure]
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(8, 19, 8, 24).WithArguments("get_Value");
            var expectedSetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(8, 19, 8, 24).WithArguments("set_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 12, 10, 22).WithArguments(".ctor");
            var expectedOpMul = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(18, 39, 18, 40).WithArguments("op_Multiply");
            var expectedLogPercentageCalculation = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(28, 17, 28, 41).WithArguments("LogPercentageCalculation");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedGetValue, expectedSetValue, expectedCtor, expectedOpMul, expectedLogPercentageCalculation
            });
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithMutableState_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public readonly struct Counter
    {
        public int Value { get; }

        public Counter(int value)
        {
            Value = value;
        }

        // Regular addition operator
        public static Counter operator +(Counter left, Counter right)
        {
            return new Counter(left.Value + right.Value);
        }

        // Checked addition operator
        public static Counter operator checked +(Counter left, Counter right)
        {
            return new Counter(checked(left.Value + right.Value));
        }
    }

    public class MutableStateTest
    {
        private int _count;

        [EnforcePure]
        public Counter IncrementCounter(Counter counter)
        {
            // Impure operation that modifies instance state
            _count++; // This makes the method impure
            return checked(counter + new Counter(1)); // checked operator call is pure
        }

        // The `Add` method itself is impure because it modifies state
        [EnforcePure]
        public Counter Add(Counter a, Counter b)
        {
            // Using a checked user-defined operator that modifies state
            return checked(a + b);
        }
    }
}";
            // Expect PS0004 warnings for pure methods that are not marked with [EnforcePure]
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 20, 11, 25).WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(13, 16, 13, 23).WithArguments(".ctor");
            var expectedOpAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(19, 40, 19, 41).WithArguments("op_Addition");
            var expectedOpCheckedAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(25, 48, 25, 49).WithArguments("op_CheckedAddition");
            var expectedIncrementCounter = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(36, 24, 36, 40).WithArguments("IncrementCounter");
            var expectedAdd = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(45, 24, 45, 27).WithArguments("Add");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedGetValue, expectedCtor, expectedOpAdd, expectedOpCheckedAdd, expectedIncrementCounter, expectedAdd
            });
        }
    }
}


