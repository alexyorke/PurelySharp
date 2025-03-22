using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FileLocalTypesTests
    {
        [Test]
        public async Task FileLocalClass_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class FileLocalCalculator
    {
        [EnforcePure]
        public int Square(int number)
        {
            return number * number;
        }
    }

    public class Application
    {
        public int CalculateSquare(int number)
        {
            var calculator = new FileLocalCalculator();
            return calculator.Square(number);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalStruct_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file struct Point
    {
        public double X { get; init; }
        public double Y { get; init; }

        [EnforcePure]
        public double CalculateDistance(Point other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public class Application
    {
        public double GetDistance(double x1, double y1, double x2, double y2)
        {
            var p1 = new Point { X = x1, Y = y1 };
            var p2 = new Point { X = x2, Y = y2 };
            return p1.CalculateDistance(p2);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalRecord_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file record Person(string FirstName, string LastName)
    {
        [EnforcePure]
        public string GetFullName()
        {
            return $""{FirstName} {LastName}"";
        }
    }

    public class Application
    {
        public string GetPersonFullName(string firstName, string lastName)
        {
            var person = new Person(firstName, lastName);
            return person.GetFullName();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalClass_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class Logger
    {
        [EnforcePure]
        public void LogMessage(string message)
        {
            // Impure operation: writing to a file
            File.AppendAllText(""log.txt"", message);
        }
    }

    // Application uses the file-local type internally but doesn't expose it
    // so we don't expect the CS9051 error
    file class Program
    {
        public void Log(string message)
        {
            var logger = new Logger();
            logger.LogMessage(message);
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(16, 13, 16, 51)
                    .WithArguments("LogMessage")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}