using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

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
using PurelySharp.Attributes;



public struct Celsius
{
    public double Value { get; }

    public Celsius(double value)
    {
        Value = value;
    }

    // REMOVED [EnforcePure]
    public static implicit operator double(Celsius celsius)
    {
        return celsius.Value;
    }

    // REMOVED [EnforcePure]
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
using PurelySharp.Attributes;



public class Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    // REMOVED [EnforcePure]
    public static explicit operator decimal(Money money)
    {
        return money.Amount;
    }

    // REMOVED [EnforcePure]
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
using PurelySharp.Attributes;



public class Counter
{
    private static int _conversionCount = 0;
    
    public int Value { get; }

    public Counter(int value)
    {
        Value = value;
    }

    // REMOVED [EnforcePure] and ADDED Roslyn markup
    public static explicit operator int(Counter counter)
    {
        _conversionCount++; // Impure operation - modifies static field
        return counter.Value;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ComplexConversion_ImpureParsing_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



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

    // REMOVED [EnforcePure]
    public static explicit operator string(DateOnly date)
    {
        return $""{date.Year}-{date.Month}-{date.Day}"";
    }

    // REMOVED [EnforcePure] and ADDED Roslyn markup
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


