using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UserDefinedConversionTests
    {
        [Test]
        public async Task ImplicitConversion_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public struct Celsius
{
    public double Value { get; }

    public Celsius(double value)
    {
        Value = value;
    }

    [EnforcePure]
    public static implicit operator double(Celsius celsius)
    {
        return celsius.Value;
    }

    [EnforcePure]
    public static implicit operator Celsius(double value)
    {
        return new Celsius(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExplicitConversion_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    [EnforcePure]
    public static explicit operator decimal(Money money)
    {
        return money.Amount;
    }

    [EnforcePure]
    public static explicit operator Money(decimal amount)
    {
        return new Money(amount, ""USD"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureConversion_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Counter
{
    private static int _conversionCount = 0;
    
    public int Value { get; }

    public Counter(int value)
    {
        Value = value;
    }

    [EnforcePure]
    public static explicit operator int(Counter counter)
    {
        _conversionCount++; // Impure operation - modifies static field
        return counter.Value;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(21, 9, 21, 27)
                .WithArguments("op_Explicit");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ComplexConversion_UnknownPurityDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class DateOnly
{
    public int Year { get; }
    public int Month { get; }
    public int Day { get; }

    public DateOnly(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
    }

    [EnforcePure]
    public static explicit operator string(DateOnly date)
    {
        return $""{date.Year}-{date.Month}-{date.Day}"";
    }

    [EnforcePure]
    public static explicit operator DateOnly(string dateString)
    {
        // Simple parsing without exception handling for test simplicity
        var parts = dateString.Split('-');
        return new DateOnly(
            int.Parse(parts[0]), 
            int.Parse(parts[1]), 
            int.Parse(parts[2]));
    }
}";

            // Expect PMA0002 because int.Parse/string.Split is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(30, 21, 30, 42) // Corrected span based on actual diagnostic
                .WithArguments("op_Explicit");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


