using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;
using System.Linq;
using System.Collections.Generic;

namespace PurelySharp.Test
{
    [TestFixture]
    public class PureStateInteractionTests
    {
        [Test]
        public async Task PureInteractionsWithState_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public abstract class Shape
{
    public int Id { get; }
    protected Shape(int id) { Id = id; }

    [EnforcePure]
    public abstract double CalculateArea();

    [EnforcePure]
    public int GetId() => Id;
}

public class Circle : Shape
{
    public double Radius { get; }
    private static readonly double PI = Math.PI;

    public Circle(int id, double radius) : base(id)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        Radius = radius;
    }

    [EnforcePure]
    public override double CalculateArea() => PI * Radius * Radius;

    [EnforcePure]
    public Circle CreateScaledCopy(double factor)
    {
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
        return new Circle(this.Id, this.Radius * factor);
    }

    [EnforcePure]
    public static double GetPi() => PI;
}

public class TestClass
{
    [EnforcePure]
    public double GetCircleArea(Circle c) => c.CalculateArea();

    [EnforcePure]
    public double GetScaledArea(Circle c, double factor)
    {
        Circle scaled = c.CreateScaledCopy(factor);
        return scaled.CalculateArea();
    }

     [EnforcePure]
    public double GetStaticPi() => Circle.GetPi();
}
";

            var expectedGetId = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 18).WithArguments("get_Id");
            var expectedShapeCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 15, 8, 20).WithArguments(".ctor");
            var expectedCircleCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(22, 12, 22, 18).WithArguments(".ctor");
            var expectedGetRadius = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(19, 19, 19, 25).WithArguments("get_Radius");

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedGetId,
                                             expectedShapeCtor,
                                             expectedGetRadius,
                                             expectedCircleCtor
                                             );
        }
    }
}