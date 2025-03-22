using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            // Pure operation with checked operator
            return checked(a + b);
        }

        [EnforcePure]
        public Money CalculateOrderTotal(Money[] prices, decimal taxRate)
        {
            // Pure operation with multiple checked operators
            Money total = new Money(0);
            foreach (var price in prices)
            {
                total = checked(total + price);
            }
            return checked(total * (1 + taxRate));
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithRegularOperator_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            // Use either checked or unchecked operator based on parameter
            return useChecked ? checked(a + b) : a + b;
        }

        [EnforcePure]
        public double CalculateDistance(Vector2D a, Vector2D b)
        {
            // Use checked operator in a pure computation
            Vector2D difference = checked(a - b);
            return difference.Magnitude;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_ComplexExpression_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public readonly struct BigInteger
    {
        private readonly int[] _digits;

        public BigInteger(int value)
        {
            _digits = new int[] { value };
        }

        // Private constructor for internal use
        private BigInteger(int[] digits)
        {
            _digits = digits;
        }

        // Regular addition
        public static BigInteger operator +(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = left._digits[0] + right._digits[0];
            return new BigInteger(new int[] { result });
        }

        // Checked addition
        public static BigInteger operator checked +(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = checked(left._digits[0] + right._digits[0]);
            return new BigInteger(new int[] { result });
        }

        // Regular multiplication
        public static BigInteger operator *(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = left._digits[0] * right._digits[0];
            return new BigInteger(new int[] { result });
        }

        // Checked multiplication
        public static BigInteger operator checked *(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = checked(left._digits[0] * right._digits[0]);
            return new BigInteger(new int[] { result });
        }

        // Regular subtraction
        public static BigInteger operator -(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = left._digits[0] - right._digits[0];
            return new BigInteger(new int[] { result });
        }

        // Checked subtraction
        public static BigInteger operator checked -(BigInteger left, BigInteger right)
        {
            // Simplified implementation for testing
            int result = checked(left._digits[0] - right._digits[0]);
            return new BigInteger(new int[] { result });
        }

        // ToString override for display
        public override string ToString()
        {
            return _digits[0].ToString();
        }
    }

    public class ComplexCheckedOperationsTest
    {
        [EnforcePure]
        public BigInteger CalculatePolynomial(BigInteger x, BigInteger a, BigInteger b, BigInteger c)
        {
            // Complex expression with multiple checked operations: ax² + bx + c
            return checked(checked(a * x * x) + checked(b * x) + c);
        }

        [EnforcePure]
        public BigInteger FibonacciChecked(int n)
        {
            // Pure recursive computation with checked operations
            if (n <= 1)
                return new BigInteger(n);

            BigInteger a = new BigInteger(0);
            BigInteger b = new BigInteger(1);
            
            for (int i = 2; i <= n; i++)
            {
                BigInteger temp = b;
                b = checked(a + b);
                a = temp;
            }
            
            return b;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithExceptionHandling_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            try
            {
                // Attempt a checked operation that might throw OverflowException
                return multiply ? checked(a * b) : checked(a + b);
            }
            catch (OverflowException)
            {
                // Handle exception in a pure way
                return new SafeInteger(0);
            }
        }

        [EnforcePure]
        public (bool Success, SafeInteger Result) SafeAdd(SafeInteger a, SafeInteger b)
        {
            // Pure method with exception handling for checked operations
            try
            {
                return (true, checked(a + b));
            }
            catch (OverflowException)
            {
                return (false, new SafeInteger(0));
            }
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public readonly struct Percentage
    {
        public double Value { get; }

        public Percentage(double value)
        {
            Value = value;
        }

        // Regular multiplication operator
        public static Percentage operator *(Percentage left, double right)
        {
            return new Percentage(left.Value * right);
        }

        // Checked multiplication operator
        public static Percentage operator checked *(Percentage left, double right)
        {
            return new Percentage(checked(left.Value * right));
        }
    }

    public class ImpureCheckedOperationsTest
    {
        [EnforcePure]
        public void LogPercentageCalculation(Percentage initial, double multiplier)
        {
            // Impure operation despite using checked operator
            Percentage result = checked(initial * multiplier);
            File.WriteAllText(""calculation.log"", $""Result: {result.Value}"");
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(39, 13, 39, 76)
                .WithArguments("LogPercentageCalculation");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task CheckedUserDefinedOperator_WithMutableState_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            _count++;
            return checked(counter + new Counter(1));
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(39, 13, 39, 21)
                .WithArguments("IncrementCounter");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}