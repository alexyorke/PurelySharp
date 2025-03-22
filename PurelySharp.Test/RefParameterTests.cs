using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RefParameterTests
    {
        [Test]
        public async Task PureMethodWithReadOnlyRefParameter_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(in int x)
    {
        return x + 10;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public void TestMethod(ref int x)
    {
        x += 10; // Impure operation - modifying a ref parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(12, 11, 12, 13).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithOutParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public void TestMethod(out int x)
    {
        x = 10; // Impure operation - writing to an out parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(12, 11, 12, 12).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithRefReadOnlyParameterAccess_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public struct Point
{
    public int X { get; }
    public int Y { get; }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public class TestClass
{
    [Pure]
    public int TestMethod(in Point p)
    {
        return p.X + p.Y; // This should be pure - only reading from 'in' parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}