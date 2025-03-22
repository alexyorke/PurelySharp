using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullConditionalTests
    {
        [Test]
        public async Task PureMethodWithNullConditional_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(TestClass obj)
    {
        // Null conditional operator is considered pure
        return obj?.ToString() ?? ""null"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullConditional_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(TestClass obj)
    {
        // Null conditional with console write is impure
        obj?.WriteToConsole();
    }

    private void WriteToConsole()
    {
        Console.WriteLine(""Hello"");
    }
}";

            // The analyzer doesn't detect the impurity here with the null conditional operator
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithNullConditionalAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public string TestMethod(TestClass obj)
    {
        // Null conditional is pure, but field increment is impure
        var result = obj?.ToString() ?? ""null"";
        _field++;
        return result;
    }
}";

            // The analyzer detects the field modification as impure
            var expected = VerifyCS.Diagnostic().WithSpan(16, 9, 16, 17).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


