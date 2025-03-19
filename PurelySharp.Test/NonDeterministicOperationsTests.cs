using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharp>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NonDeterministicOperationsTests
    {
        [Test]
        public async Task ImpureMethodWithRandomOperation_Diagnostic()
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
        return new Random().Next();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}