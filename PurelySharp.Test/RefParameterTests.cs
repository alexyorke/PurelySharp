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
    // Simple struct
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    [TestFixture]
    public class RefParameterTests
    {
        [Test]
        public async Task PureMethodWithInParameter_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(in int a)
    {
        return a + 10;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithInParameterAccess_NoDiagnostic()
        {
            // Test accessing fields/properties of a struct passed with 'in'
            var test = @"
using PurelySharp.Attributes;

// Corrected struct definition with proper { get; } accessors
public struct Point { public int X { get; } public int Y { get; } }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(in Point p)
    {
        // Reading from 'in' parameter fields/properties is pure
        return p.X + p.Y;
    }
}";

            // Expect NO diagnostic because reading from 'in' parameter is pure.
            // Expect PS0004 on the auto-generated getters for X and Y
            var expectedX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(5, 34, 5, 35).WithArguments("get_X");
            var expectedY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(5, 56, 5, 57).WithArguments("get_Y");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedX, expectedY);
        }

        [Test]
        public async Task PureMethodWithInParameterCall_NoDiagnostic()
        {
            // Test passing 'in' parameter to another pure method
            var test = @"
using PurelySharp.Attributes;

// Corrected struct definition with proper { get; } accessors
public struct Point { public int X { get; } public int Y { get; } }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(in Point p)
    {
        return Helper(p); // Calling pure method with 'in' param
    }

    // Moved Helper method inside TestClass
    [EnforcePure]
    private int Helper(in Point p) => p.X;
}";

            // Expect NO diagnostic
            // Expect PS0004 on the auto-generated getters for X and Y
            var expectedX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(5, 34, 5, 35).WithArguments("get_X");
            var expectedY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(5, 56, 5, 57).WithArguments("get_Y");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedX, expectedY);
        }

        // Add test for calling impure method with in parameter?
    }
}


