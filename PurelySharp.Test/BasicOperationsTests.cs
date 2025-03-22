using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicOperationsTests
    {
        [Test]
        public async Task EmptyMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public void TestMethod()
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithReturn_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x, int y)
    {
        return x + y;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithLocalVar_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x, int y)
    {
        var result = x * y;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Add(int x, int y)
    {
        return x + y;
    }

    [Pure]
    public int AddAndMultiply(int x, int y, int z)
    {
        return Add(x, y) * z;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RecursivePureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Fibonacci(int n)
    {
        if (n <= 1) return n;
        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}