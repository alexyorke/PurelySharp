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
        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) for pure operator overloads lacking [EnforcePure].
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

            var expectedX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 18, 7, 19).WithArguments("get_X");
            var expectedY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 18, 8, 19).WithArguments("get_Y");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 12, 10, 19).WithArguments(".ctor");
            var expectedAdd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(16, 36, 16, 37).WithArguments("op_Addition");
            var expectedSub = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(21, 36, 21, 37).WithArguments("op_Subtraction");
            var expectedMul = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(26, 36, 26, 37).WithArguments("op_Multiply");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedX, expectedY, expectedCtor, expectedAdd, expectedSub, expectedMul);
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

    // This operator is impure because it modifies static state
    [EnforcePure]
    public static Counter operator +(Counter a, Counter b)
    {
        _totalOperations++; 
        return new Counter(a.Value + b.Value);
    }
}";
            var expectedVal = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(9, 16, 9, 21).WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 12, 11, 19).WithArguments(".ctor");
            var expectedOp = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(18, 36, 18, 37).WithArguments("op_Addition");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedVal, expectedCtor, expectedOp);
        }

        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) for pure operator overloads lacking [EnforcePure].
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

            var expectedGet = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 26).WithArguments("get_Celsius");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(9, 12, 9, 23).WithArguments(".ctor");
            var expectedLess = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(14, 33, 14, 34).WithArguments("op_LessThan");
            var expectedGreater = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(19, 33, 19, 34).WithArguments("op_GreaterThan");
            var expectedEqual = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(24, 33, 24, 35).WithArguments("op_Equality");
            var expectedNotEqual = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(29, 33, 29, 35).WithArguments("op_Inequality");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedGet, expectedCtor, expectedLess, expectedGreater, expectedEqual, expectedNotEqual);
        }

        // Expectation limitation: analyzer currently does not report missing enforce-pure-attribute diagnostic (PS0004) for pure operator overloads lacking [EnforcePure].
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

            var expectedMeterVal = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 24).WithArguments("get_Value");
            var expectedMeterCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 12, 8, 17).WithArguments(".ctor");
            var expectedFootVal = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(18, 19, 18, 24).WithArguments("get_Value");
            var expectedFootCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(19, 12, 19, 16).WithArguments(".ctor");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedMeterVal, expectedMeterCtor, expectedFootVal, expectedFootCtor);
        }
    }
}


