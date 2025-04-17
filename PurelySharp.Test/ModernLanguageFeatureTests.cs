using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ModernLanguageFeatureTests
    {
        [Test]
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

        [Test]
        public async Task MethodWithNullCoalescing_Diagnostic()
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

            // Expect PMA0002 because ToUpper() might depend on culture.
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(12, 22, 12, 32) // Span of .ToUpper()
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
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

        [Test]
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

        [Test]
        public async Task MethodWithStackallocAndSpan_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public unsafe int ProcessData()
    {
        int sum = 0;
        Span<byte> buffer = stackalloc byte[10];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)i;
            sum += buffer[i];
        }
        return sum;
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 13, 16, 32).WithArguments("ProcessData");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
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


