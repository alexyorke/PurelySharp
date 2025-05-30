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
    public class MathOperationsTests
    {
        [Test]
        public async Task ComplexNestedExpressions_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x, double y, double z)
    {
        var a = Math.Sin(x) * Math.Cos(y);
        var b = Math.Pow(Math.E, z) / Math.PI; // Pure: Math.E/PI allowed
        var c = Math.Sqrt(Math.Abs(a * b));
        return Math.Max(a, Math.Min(b, c));
    }
}";
            // Expect no diagnostic now
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SimpleMathMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(x);
    }
}";
            // Expect no diagnostic as Math.Sin is pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Correctly expects no diagnostic
        }

        [Test]
        public async Task MathConstant_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod()
    {
        return Math.PI; // Pure: Math.PI allowed
    }
}";
            // Expect no diagnostic now
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MathMethodChain_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(Math.Cos(x));
    }
}";

            // Assuming System.Math is pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Removed PS0002 expectation
        }
    }
}


