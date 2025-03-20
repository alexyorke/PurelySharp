using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LocalFunctionAndRecursionTests
    {
        [Test]
        public async Task PureLocalFunction_NoDiagnostic()
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
                .WithLocation(16, 13)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
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
                .WithLocation(13, 9)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
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

            // The analyzer doesn't detect pure method calling impure method
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}