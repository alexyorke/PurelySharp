using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class LocalFunctionAndRecursionTests
    {
        [TestMethod]
        public async Task MethodWithLocalFunction_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public int TestMethod()
    {
        int LocalFunction()
        {
            _field++; // Local function modifies field
            return _field;
        }

        return LocalFunction();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithRecursiveImpureCall_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(int n)
    {
        if (n <= 0) return;
        Console.WriteLine(n); // Impure operation
        TestMethod(n - 1); // Recursive call
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private void ImpureMethod()
    {
        Console.WriteLine(""Impure"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        ImpureMethod(); // Calling impure method
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}