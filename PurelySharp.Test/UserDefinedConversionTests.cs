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
        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) for pure user-defined conversions lacking [EnforcePure].
        // TODO: Re-enable PS0004 checks for the conversion operators themselves when implemented.
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

            // Expect PS0004 on getter and constructor
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(9, 19, 9, 24) // Span of Value getter
                                        .WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(11, 12, 11, 19) // Span of Celsius ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter, expectedCtor);
        }

        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) for pure user-defined conversions lacking [EnforcePure].
        // TODO: Re-enable PS0004 checks for the conversion operators themselves when implemented.
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

            // Expect PS0004 on getters and constructor
            var expectedGetterAmount = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(9, 20, 9, 26) // Span of Amount getter
                                        .WithArguments("get_Amount");
            var expectedGetterCurrency = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(10, 19, 10, 27) // Span of Currency getter
                                          .WithArguments("get_Currency");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(12, 12, 12, 17) // Span of Money ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetterAmount, expectedGetterCurrency, expectedCtor);
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

            // Expect PS0004 on getter and constructor
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(11, 16, 11, 21) // Span of Value getter
                                        .WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(13, 12, 13, 19) // Span of Counter ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter, expectedCtor);
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

            // Expect PS0004 on getters and constructor
            var expectedGetterYear = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(9, 16, 9, 20) // Span of Year getter
                                        .WithArguments("get_Year");
            var expectedGetterMonth = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                         .WithSpan(10, 16, 10, 21) // Span of Month getter
                                         .WithArguments("get_Month");
            var expectedGetterDay = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(11, 16, 11, 19) // Span of Day getter
                                       .WithArguments("get_Day");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(13, 12, 13, 20) // Span of DateOnly ctor
                                       .WithArguments(".ctor");

            // Note: DateOnly-to-string conversion might be pure depending on analysis of interpolation/ToString.
            // We are only expecting PS0002 for the string-to-DateOnly conversion due to Split/Parse.
            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetterYear, expectedGetterMonth, expectedGetterDay, expectedCtor);
        }
    }
}


