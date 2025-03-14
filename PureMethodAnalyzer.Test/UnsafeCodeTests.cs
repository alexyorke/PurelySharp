using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class UnsafeCodeTests
    {
        [TestMethod]
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

        [TestMethod]
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
                .WithLocation(10, 24)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}