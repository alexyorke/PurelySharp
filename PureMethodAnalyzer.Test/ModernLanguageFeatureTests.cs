using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class ModernLanguageFeatureTests
    {
        [TestMethod]
        public async Task MethodWithPatternMatching_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(object obj)
    {
        return obj switch
        {
            int i => $""Integer: {i}"",
            string s => $""String: {s}"",
            _ => ""Unknown type""
        };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithNullCoalescing_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        return input?.ToUpper() ?? ""EMPTY"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithConstantFolding_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private const double PI = 3.14159;
    private const int FACTOR = 2;

    [EnforcePure]
    public double TestMethod(double radius)
    {
        const double area = PI * FACTOR;
        return radius * area;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithTupleDeconstruction_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public (int, int) TestMethod((int x, int y) input)
    {
        var (a, b) = input;
        return (a * 2, b * 3);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithStackallocAndSpan_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        Span<int> span = stackalloc int[10];
        for (int i = 0; i < 10; i++)
        {
            span[i] = i * i;
        }
        return span[5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithYieldReturn_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(int n)
    {
        for (int i = 0; i < n; i++)
        {
            yield return i * i;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}