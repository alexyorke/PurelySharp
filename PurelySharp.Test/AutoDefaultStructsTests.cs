using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AutoDefaultStructsTests
    {
        [Test]
        public async Task AutoDefaultStruct_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public struct Point
    {
        public int X;
        public int Y;
        
        [EnforcePure]
        public double CalculateDistance()
        {
            // Uses auto-default field values (X and Y are automatically initialized to 0)
            return Math.Sqrt(X * X + Y * Y);
        }
    }
    
    public class PointTest
    {
        public static void TestPoint()
        {
            Point p = new Point(); // Auto-default struct initialization
            Console.WriteLine(p.CalculateDistance()); // Should output 0
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AutoDefaultStruct_WithConstructor_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public struct Vector3D
    {
        public double X;
        public double Y;
        public double Z;
        
        // Parameterless constructor allowed in C# 11
        public Vector3D()
        {
            // Only initialize Z, leaving X and Y with their default values
            Z = 1.0;
        }
        
        [EnforcePure]
        public double CalculateMagnitude()
        {
            // Pure method using auto-default values and manually set values
            return Math.Sqrt(X * X + Y * Y + Z * Z);
        }
    }
    
    public class VectorTest
    {
        public static double TestVector()
        {
            Vector3D v = new Vector3D(); // Will call the parameterless constructor
            return v.CalculateMagnitude(); // Should return 1.0 (Z=1, X=0, Y=0)
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AutoDefaultStruct_WithReadonlyFields_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public readonly struct Temperature
    {
        public readonly double Celsius; // Will be auto-defaulted to 0
        public readonly double Fahrenheit => (Celsius * 9 / 5) + 32;
        
        [EnforcePure]
        public bool IsFreezing()
        {
            // Pure method using auto-default values
            return Celsius <= 0;
        }
    }
    
    public class TemperatureTest
    {
        public static void TestTemperature()
        {
            Temperature temp = new Temperature(); // Auto-default readonly struct
            Console.WriteLine(temp.IsFreezing()); // Should output True
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AutoDefaultStruct_WithProperties_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public struct Rectangle
    {
        // Auto-properties will be auto-defaulted to 0
        public int Width { get; set; }
        public int Height { get; set; }
        
        [EnforcePure]
        public int CalculateArea()
        {
            // Pure method using auto-default property values
            return Width * Height;
        }
    }
    
    public class RectangleTest
    {
        public static void TestRectangle()
        {
            Rectangle rect = new Rectangle(); // Auto-default struct with properties
            Console.WriteLine(rect.CalculateArea()); // Should output 0
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AutoDefaultStruct_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



namespace TestNamespace
{
    public struct Logger
    {
        public string LogFileName; // Auto-defaulted to null
        
        [EnforcePure]
        public void WriteLog(string message)
        {
            // Impure operation using auto-default field
            if (LogFileName == null)
            {
                LogFileName = ""default.log"";
            }
            
            // Impure operation: file system access
            File.AppendAllText(LogFileName, message + Environment.NewLine);
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(20, 29, 20, 30)
                    .WithArguments("WriteLog")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task AutoDefaultStruct_WithNestedStructs_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



namespace TestNamespace
{
    public struct Coordinate
    {
        public double Latitude; // Auto-defaulted to 0
        public double Longitude; // Auto-defaulted to 0
    }
    
    public struct GeoLocation
    {
        public Coordinate Position; // Nested struct is also auto-defaulted
        public string Name; // Auto-defaulted to null
        
        [EnforcePure]
        public bool IsOrigin()
        {
            // Pure method using auto-default values in nested struct
            return Position.Latitude == 0 && Position.Longitude == 0;
        }
    }
    
    public class GeoTest
    {
        public static void TestGeo()
        {
            GeoLocation location = new GeoLocation(); // Auto-default with nested struct
            Console.WriteLine(location.IsOrigin()); // Should output True
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


