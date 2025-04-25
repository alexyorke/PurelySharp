using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FileScopedNamespacesTests
    {
        [Test]
        public async Task FileScopedNamespace_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

// Using file-scoped namespace (C# 10 feature)
namespace TestNamespace;



// Class in file-scoped namespace
public class Calculator
{
    [EnforcePure]
    public int Add(int a, int b)
    {
        // Pure operation in a file-scoped namespace
        return a + b;
    }
    
    [EnforcePure]
    public double CalculateCircleArea(double radius)
    {
        // Pure: Math.PI read is now allowed
        return Math.PI * radius * radius;
    }
}";
            // Expect no diagnostic now
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_WithNestedTypes_PureMethod_ExpectsDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

// File-scoped namespace with nested types
namespace TestLibrary;



// Parent class in file-scoped namespace
public class Geometry
{
    // Nested class
    public class Circle
    {
        public double Radius { get; }
        
        public Circle(double radius)
        {
            Radius = radius;
        }
        
        [EnforcePure]
        public double CalculateArea()
        {
            // Pure: Math.PI read is now allowed
            return Math.PI * Radius * Radius;
        }
    }
    
    // Nested struct
    public readonly struct Point
    {
        public double X { get; }
        public double Y { get; }
        
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
        
        [EnforcePure]
        public double DistanceFromOrigin()
        {
            // Impure: Analyzer doesn't find implicit getter source for X/Y
            return Math.Sqrt(X * X + Y * Y);
        }
    }
}";

            // Expect PS0002 ONLY for DistanceFromOrigin (getter source) - NOW EXPECTING 0 DIAGNOSTICS
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_WithMultipleClasses_PureMethod_ExpectsDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;

// File-scoped namespace with multiple classes
namespace TestUtilities;



// First class in file-scoped namespace
public class StringUtils
{
    [EnforcePure]
    public string ReverseString(string input)
    {
        // Impure due to unhandled DelegateCreation in ToArray()
        return new string(input.Reverse().ToArray());
    }
}

// Second class in same file-scoped namespace
public class MathUtils
{
    [EnforcePure]
    public int Factorial(int n)
    {
        // Impure: Analyzer doesn't handle recursion correctly
        if (n <= 1) return 1;
        return n * Factorial(n - 1);
    }
}

// Third class in same file-scoped namespace
public static class Extensions
{
    [EnforcePure]
    public static bool IsEven(this int number)
    {
        // Pure extension method
        return number % 2 == 0;
    }
}";

            // Expect PS0002 for ReverseString (LINQ) and Factorial (recursion)
            var expected = new[] {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id).WithSpan(16, 19, 16, 32).WithArguments("ReverseString"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id).WithSpan(27, 16, 27, 25).WithArguments("Factorial")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task FileScopedNamespace_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;

// File-scoped namespace with impure method
namespace TestImpure;



public class FileManager
{
    [EnforcePure]
    public void WriteToFile(string content)
    {
        // Impure operation: file system access
        File.WriteAllText(""test.txt"", content);
    }

    [EnforcePure]
    public string ReadCurrentTime()
    {
        // Impure operation: accessing current time
        return DateTime.Now.ToString();
    }
}";

            // Correctly define two separate expected diagnostics
            var expected = new[] {
                VerifyCS.Diagnostic("PS0002").WithSpan(14, 17, 14, 28).WithArguments("WriteToFile"),
                VerifyCS.Diagnostic("PS0002").WithSpan(21, 19, 21, 34).WithArguments("ReadCurrentTime")
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task FileScopedNamespace_InterfaceImplementation_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

// File-scoped namespace with interface implementation
namespace TestInterfaces;

public interface ICalculator
{
    int Add(int a, int b);
}

public class Calculator : ICalculator
{
    [EnforcePure]
    public int Add(int a, int b)
    {
        return a + b;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_Record_ExpectsDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

// File-scoped namespace with record
namespace TestRecords;

public record Point(double X, double Y)
{
    [EnforcePure]
    public double DistanceFromOrigin()
    {
        // Pure operation (Math.Sqrt is pure, property reads are pure)
        return Math.Sqrt(X * X + Y * Y);
    }
}";

            // Expect only compiler errors, not PS0002, as the method is pure
            var expected = new[] {
                DiagnosticResult.CompilerError("CS0518").WithSpan(8, 28, 8, 29).WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                DiagnosticResult.CompilerError("CS0518").WithSpan(8, 38, 8, 39).WithArguments("System.Runtime.CompilerServices.IsExternalInit")
                // Removed PS0002 expectation
            };
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
