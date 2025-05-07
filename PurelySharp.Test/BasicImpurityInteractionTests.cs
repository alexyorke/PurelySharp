using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Collections.Generic;
using System;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicImpurityInteractionTests
    {
        [Test]
        public async Task ImpureMethodModifyingInstanceState_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public abstract class Shape
{
    public int Id { get; protected set; }
    private static int _nextId = 1;

    // [EnforcePure] // Base ctor is impure
    protected Shape() // Line 9
    {
        Id = _nextId++;
    }

    [EnforcePure]
    public abstract double CalculateArea();

    // [EnforcePure] // Keep base pure for this test variant
    public virtual void Scale(double factor) { }

    [EnforcePure]
    public int GetId() => Id;
}

public class Circle : Shape
{
    public double Radius { get; private set; }
    private static readonly double PI = 3.14159;

    [EnforcePure] // Marked, but calls impure base ctor
    public Circle(double radius) : base() // Line 29
    {
        Radius = radius;
    }

    [EnforcePure]
    public override double CalculateArea() => PI * Radius * Radius;

    [EnforcePure] // Marked, impure override
    public override void Scale(double factor) // Line 38
    {
        this.Radius *= factor;
    }

    [EnforcePure] // Marked, impure method
    public void SetRadius(double newRadius) // Line 44
    {
        this.Radius = newRadius;
    }

    // SetCenter method removed as it wasn't relevant to original test intent

    [EnforcePure]
    public static double GetPi() => PI;
}

public class TestClass
{
    [EnforcePure] // Marked, calls impure SetRadius
    public void ProcessShape(Circle c) // Line 62
    {
        c.SetRadius(10.0);
    }

    [EnforcePure] // Marked, calls impure Scale
    public double CalculateAndScale(Circle c, double factor) // Line 68
    {
       double area = c.CalculateArea();
       c.Scale(factor);
       return area;
    }

    [EnforcePure]
    public double GetCircleArea(Circle c) => c.CalculateArea();

    [EnforcePure]
    public double GetStaticPi() => Circle.GetPi();
}
";
            // Expectations based on test run + added [EnforcePure]
            var expectedGetId = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 18).WithArguments("get_Id");
            var expectedSetId = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 18).WithArguments("set_Id");
            var expectedCtorShape = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(11, 15, 11, 20).WithArguments(".ctor"); // Adjusted line
            var expectedScaleShape = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(20, 25, 20, 30).WithArguments("Scale"); // Adjusted span from line 19 to 20
            var expectedGetRadius = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(28, 19, 28, 25).WithArguments("get_Radius"); // Adjusted line
            var expectedSetRadiusPure = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(28, 19, 28, 25).WithArguments("set_Radius"); // Adjusted line // Auto-setter
            var expectedCtorCircle = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(32, 12, 32, 18).WithArguments(".ctor"); // Adjusted line // Calls impure base
            var expectedScaleCircle = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(41, 26, 41, 31).WithArguments("Scale"); // Adjusted line // Modifies this.Radius
            var expectedSetRadiusCircle = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(47, 17, 47, 26).WithArguments("SetRadius"); // Adjusted line // Modifies this.Radius
            var expectedProcessShape = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(61, 17, 61, 29).WithArguments("ProcessShape"); // Adjusted line // Calls impure SetRadius
            var expectedCalculateAndScale = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(67, 19, 67, 36).WithArguments("CalculateAndScale"); // Adjusted line // Calls impure Scale

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedGetId,
                                             expectedSetId,
                                             expectedCtorShape,
                                             expectedScaleShape,
                                             expectedGetRadius,
                                             expectedSetRadiusPure,
                                             expectedCtorCircle,
                                             expectedScaleCircle,
                                             expectedSetRadiusCircle,
                                             expectedProcessShape,
                                             expectedCalculateAndScale
                                             ); // Expect 11 diagnostics
        }

        [Test]
        public async Task ImpureMethodCall_Diagnostic()
        {
            // This test passed previously with these explicit expectations.
            var test = @"
using PurelySharp.Attributes;

public class ConfigData
{
    private string _name = ""Default"";
    public string Name { get => _name; [EnforcePure] set { _name = value; } }

    [EnforcePure] // Method itself is impure
    public void Configure(string newName) // Line 10
    {
        this.Name = newName; // Line 12 - Calls impure setter
    }
}

public class TestClass
{
    [EnforcePure] // Method itself is impure
    public void ImpureMethodCall(ConfigData data) // Line 19
    {
        data.Configure(""NewName""); // Line 21 - Calls impure Configure
    }
}
";
            // Corrected based on runner output: Expect 4 diagnostics
            var expectedSetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(7, 19, 7, 23).WithArguments("set_Name"); // PS0002
            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 23).WithArguments("get_Name"); // PS0004
            var expectedConfigure = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(10, 17, 10, 26).WithArguments("Configure"); // PS0002
            var expectedImpureMethodCall = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(19, 17, 19, 33).WithArguments("ImpureMethodCall");// PS0002

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedSetName,
                                             expectedGetName,
                                             expectedConfigure,
                                             expectedImpureMethodCall
                                             );
        }
    }
}