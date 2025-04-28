using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

// Polyfill for IsExternalInit required by records with init properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace PurelySharp.Test
{
    [TestFixture]
    public class PrimaryConstructorTests
    {
        // Note: These tests require C# 12+

        [Test]
        public async Task PureMethodWithPrimaryConstructor_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

// Class with primary constructor that initializes readonly fields
public class Calculator(int initialValue)
{
    private readonly int _initialValue = initialValue;

    [EnforcePure]
    public int Add(int x)
    {
        // Pure method that uses field initialized via primary constructor
        return _initialValue + x;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithPrimaryConstructor_AssignmentToNonReadonlyField_Diagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

// Class with primary constructor that initializes a non-readonly field
public class Calculator(int initialValue)
{
    private int _value = initialValue;

    [EnforcePure]
    public int AddAndStore(int x)
    {
        // Impure method that modifies a field
        _value += x;
        return _value;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(13, 16, 13, 27).WithArguments("AddAndStore"));
        }

        [Test]
        public async Task RecordWithPrimaryConstructorIsPure_NoDiagnostic()
        {
            var testCode = @"
// Requires C# 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

// Record using primary constructor
public record GreetingRecord(string Message)
{
    [EnforcePure]
    // Primary constructor parameter access is now correctly handled as pure
    public string GetGreeting() => $""Hello, {Message}!"";
}";
            var verifierTest = new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.AddMetadataReference(
                            projectId,
                            MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                }
            };
            verifierTest.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await verifierTest.RunAsync();
        }

        [Test]
        public async Task StructWithPrimaryConstructor_NoDiagnostic()
        {
            var testCode = @"
// Requires C# 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

// Struct using primary constructor
public readonly struct Vector2D(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;

    [EnforcePure]
    public double Length() => Math.Sqrt(X * X + Y * Y);
}";
            var verifierTest = new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.AddMetadataReference(
                            projectId,
                            MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                }
            };
            verifierTest.ExpectedDiagnostics.AddRange(DiagnosticResult.EmptyDiagnosticResults);
            await verifierTest.RunAsync();
        }

        [Test]
        public async Task ClassWithPrimaryConstructorAndInitialization_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

// Class with primary constructor and initialization
public class Rectangle(double width, double height)
{
    private readonly double _width = width;
    private readonly double _height = height;
    private readonly double _area = width * height;

    [EnforcePure]
    public double GetArea()
    {
        // Pure method that uses readonly field initialized in constructor
        return _area;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ClassWithPrimaryConstructor_ImpureMethod_Diagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

// Class with primary constructor
public class LoggingCalculator(int initialValue)
{
    private readonly int _initialValue = initialValue;

    [EnforcePure]
    public int Add(int x)
    {
        Console.WriteLine($""Adding {x} to {_initialValue}"");
        return _initialValue + x;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(13, 16, 13, 19).WithArguments("Add"));
        }
    }
}
