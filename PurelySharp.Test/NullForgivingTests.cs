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
    public class NullForgivingTests
    {
        [Test]
        public async Task PureMethodWithNullForgiving_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(string input)
    {
        // Null forgiving operator is considered pure
        return input!.Length;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullForgiving_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(string input)
    {
        // Null forgiving with console write is impure
        Console.WriteLine(input!);
    }
}";

            // The analyzer should detect the Console.WriteLine as impure
            var expected = VerifyCS.Diagnostic().WithSpan(13, 9, 13, 34).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithNullForgivingAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public int TestMethod(string input)
    {
        // Null forgiving is pure, but field increment is impure
        var length = input!.Length;
        _field++;
        return length;
    }
}";

            // The analyzer should detect the field modification as impure
            var expected = VerifyCS.Diagnostic().WithSpan(16, 9, 16, 17).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


