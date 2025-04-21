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
    public class OperatorOverloadTests
    {
        [Test]
        public async Task PureOperatorOverload_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Vector2
{
    public float X { get; }
    public float Y { get; }

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 operator +(Vector2 a, Vector2 b)
    {
        return new Vector2(a.X + b.X, a.Y + b.Y);
    }

    public static Vector2 operator -(Vector2 a, Vector2 b)
    {
        return new Vector2(a.X - b.X, a.Y - b.Y);
    }

    public static Vector2 operator *(Vector2 a, float scalar)
    {
        return new Vector2(a.X * scalar, a.Y * scalar);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureOperatorOverload_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class Counter
{
    private static int _totalOperations = 0;

    public int Value { get; }

    public Counter(int value)
    {
        Value = value;
    }

    public static Counter operator +(Counter a, Counter b)
    {
        _totalOperations++;
        return new Counter(a.Value + b.Value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ComparisonOperatorOverload_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Temperature
{
    public double Celsius { get; }

    public Temperature(double celsius)
    {
        Celsius = celsius;
    }

    public static bool operator <(Temperature a, Temperature b)
    {
        return a.Celsius < b.Celsius;
    }

    public static bool operator >(Temperature a, Temperature b)
    {
        return a.Celsius > b.Celsius;
    }

    public static bool operator ==(Temperature a, Temperature b)
    {
        return a.Celsius == b.Celsius;
    }

    public static bool operator !=(Temperature a, Temperature b)
    {
        return a.Celsius != b.Celsius;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConversionOperatorOverload_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Meter
{
    public double Value { get; }
    public Meter(double value) { Value = value; }

    public static explicit operator Foot(Meter meter)
    {
        return new Foot(meter.Value * 3.28084);
    }
}

public struct Foot
{
    public double Value { get; }
    public Foot(double value) { Value = value; }

    public static explicit operator Meter(Foot foot)
    {
        return new Meter(foot.Value / 3.28084);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


