using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InParameterTests
    {
        [Test]
        public async Task PureMethodWithInParameter_NoDiagnostic()
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
        return x + 10; // Reading from 'in' parameter is pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithMultipleInParameters_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public string TestMethod(in int x, in string y)
    {
        return y + x.ToString(); // Reading from multiple 'in' parameters is pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithInParameterStruct_NoDiagnostic()
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
        return p.X + p.Y; // Reading from struct fields via 'in' parameter is pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithNestedInParameter_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class Helper
{
    public static int Add(in int a, in int b)
    {
        return a + b;
    }
}

public class TestClass
{
    [Pure]
    public int TestMethod(in int x, in int y)
    {
        return Helper.Add(in x, in y); // Passing 'in' parameters to another method is pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithInOutMixedParameters_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(in int x, out int y)
    {
        y = x + 10; // Setting 'out' parameter is impure even with 'in' parameter
        return x;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(12, 11, 12, 12).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodTryingToModifyInParameter_CompilerError()
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
        x = 20; // This will cause a compiler error (CS8331)
        return x;
    }
}";

            // This should fail with a compiler error CS8331, not our analyzer
            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("CS8331").WithSpan(12, 9, 12, 10));
        }
    }
}