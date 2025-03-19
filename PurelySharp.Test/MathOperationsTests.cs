using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharp>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x, double y, double z)
    {
        var a = Math.Sin(x) * Math.Cos(y);
        var b = Math.Pow(Math.E, z) / Math.PI;
        var c = Math.Sqrt(Math.Abs(a * b));
        return Math.Max(a, Math.Min(b, c));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SimpleMathMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(x);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MathConstant_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod()
    {
        return Math.PI;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MathMethodChain_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(Math.Cos(x));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}