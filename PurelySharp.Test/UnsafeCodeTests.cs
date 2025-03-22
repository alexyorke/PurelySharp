using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UnsafeCodeTests
    {
        [Test]
        public async Task MethodWithUnsafeCode_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public unsafe void TestMethod()
    {
        int x = 42;
        int* ptr = &x;
        *ptr = 100; // Unsafe pointer operation is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 24)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithFixedStatement_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public unsafe void TestMethod()
    {
        byte[] array = new byte[10];
        fixed (byte* ptr = array) // Fixed statement is impure
        {
            *ptr = 42;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(12, 24, 12, 36)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}