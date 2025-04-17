using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RecordStructTests
    {
        [Test]
        public async Task PureRecordStruct_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record struct Point
{
    public double X { get; init; }
    public double Y { get; init; }

    [EnforcePure]
    public double DistanceTo(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [EnforcePure]
    public Point WithX(double newX) => this with { X = newX };

    [EnforcePure]
    public Point WithY(double newY) => this with { Y = newY };
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureRecordStructWithConstructor_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record struct Temperature(double Celsius)
{
    [EnforcePure]
    public double Fahrenheit() => Celsius * 9 / 5 + 32;

    [EnforcePure]
    public Temperature ToFahrenheit()
    {
        return new Temperature(Fahrenheit());
    }

    [EnforcePure]
    public static Temperature FromFahrenheit(double fahrenheit)
    {
        return new Temperature((fahrenheit - 32) * 5 / 9);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureRecordStruct_Diagnostic()
        {
            var test = @"
using System;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record struct Counter
{
    private static int _instances = 0;
    public int Value { get; init; }

    public Counter()
    {
        Value = 0;
        _instances++;
    }

    public Counter(int value)
    {
        Value = value;
        _instances++;
    }

    [EnforcePure]
    public static int GetInstanceCount()
    {
        return _instances;
    }

    [EnforcePure]
    public Counter Increment()
    {
        _instances++; // Impure operation - modifies static field
        return this with { Value = Value + 1 };
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(33, 16, 33, 26)
                    .WithArguments("GetInstanceCount"),
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(39, 9, 39, 19)
                    .WithArguments("Increment")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task RecordStructWithMutableProperty_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Immutable;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

public record struct CacheEntry(string Key, string Value)
{
    public ImmutableList<string> Tags { get; init; } = ImmutableList<string>.Empty;

    [EnforcePure]
    public CacheEntry AddTag(string tag)
    {
        // Example pure method
        return this with { Tags = Tags.Add(tag) };
    }

    [EnforcePure]
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag);
    }

    [EnforcePure]
    public CacheEntry WithTag(string tag)
    {
        // Create a new instance with an updated immutable list
        return this with { Tags = Tags.Add(tag) };
    }
}";

            // Expect CS0518 due to missing IsExternalInit 
            // Expect PMA0001 for both AddTag and WithTag due to 'with' expression
            var expected = new[] {
                DiagnosticResult.CompilerError("CS0518").WithSpan(10, 46, 10, 50).WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 35, 16, 48).WithArguments("AddTag"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(29, 35, 29, 48).WithArguments("WithTag")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


