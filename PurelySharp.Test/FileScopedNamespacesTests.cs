using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

// Using file-scoped namespace (C# 10 feature)
namespace TestNamespace;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
        // Another pure operation in file-scoped namespace
        return Math.PI * radius * radius;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_WithNestedTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

// File-scoped namespace with nested types
namespace TestLibrary;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            // Pure operation in nested class within file-scoped namespace
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
            // Pure operation in nested struct within file-scoped namespace
            return Math.Sqrt(X * X + Y * Y);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_WithMultipleClasses_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

// File-scoped namespace with multiple classes
namespace TestUtilities;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

// First class in file-scoped namespace
public class StringUtils
{
    [EnforcePure]
    public string ReverseString(string input)
    {
        // Pure operation using LINQ
        return new string(input.Reverse().ToArray());
    }
}

// Second class in same file-scoped namespace
public class MathUtils
{
    [EnforcePure]
    public int Factorial(int n)
    {
        // Pure recursive operation
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileScopedNamespace_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

// File-scoped namespace with impure method
namespace TestImpure;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(17, 9, 17, 47).WithArguments("WriteToFile"));
        }
    }
}


