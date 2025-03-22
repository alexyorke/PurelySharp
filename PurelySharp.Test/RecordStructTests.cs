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
using System.Collections.Generic;
using System.Collections.Immutable;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record struct CacheEntry
{
    public string Key { get; init; }
    public object Value { get; init; }
    public DateTime Created { get; init; }
    
    // Use immutable collection to avoid mutable state
    public ImmutableList<string> Tags { get; init; }
    
    public CacheEntry(string key, object value)
    {
        Key = key;
        Value = value;
        Created = DateTime.UtcNow;
        Tags = ImmutableList<string>.Empty;
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


