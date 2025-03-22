using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class OutParameterTests
    {
        [Test]
        public async Task PureMethodWithOutParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(out int x)
    {
        x = 10; // Impure operation - writing to an out parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(12, 11, 12, 12).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithMultipleOutParameters_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(out int x, out string y)
    {
        x = 10;
        y = ""hello"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(13, 11, 13, 12).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithTryPattern_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public bool TryParse(string input, out int result)
    {
        if (int.TryParse(input, out result))
        {
            return true;
        }
        result = 0;
        return false;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(16, 16, 16, 17).WithArguments("TryParse"));
        }

        [Test]
        public async Task PureMethodCallingMethodWithOutParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private void HelperMethod(out int x)
    {
        x = 42;
    }

    [EnforcePure]
    public int TestMethod()
    {
        int result;
        HelperMethod(out result); // Calling method with out parameter is impure
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(18, 9, 18, 33).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithOutVarDeclaration_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private void HelperMethod(out int x)
    {
        x = 42;
    }

    [EnforcePure]
    public int TestMethod()
    {
        HelperMethod(out var result); // Using out var declaration
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(17, 9, 17, 37).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithDiscardedOutParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private void HelperMethod(out int x, out int y)
    {
        x = 42;
        y = 100;
    }

    [EnforcePure]
    public int TestMethod()
    {
        int result;
        HelperMethod(out result, out _); // Using discarded out parameter
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(19, 9, 19, 40).WithArguments("TestMethod"));
        }
    }
}


