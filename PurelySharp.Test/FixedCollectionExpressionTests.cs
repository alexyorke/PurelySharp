using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FixedCollectionExpressionTests
    {
        [Test]
        public async Task PureMethod_MutableArrayWithArrayCreation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetNumbers()
    {
        // Using new[] array creation expression
        return new[] { 1, 2, 3, 4, 5 };
    }
}";

            var expected = new DiagnosticResult(
                "PMA0001",
                DiagnosticSeverity.Error)
                .WithSpan(13, 16, 13, 39)
                .WithArguments("GetNumbers");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetArray()
    {
        // Using collection expression syntax
        return [1, 2, 3, 4, 5];
    }
}";

            var expected = new DiagnosticResult(
                "PMA0001",
                DiagnosticSeverity.Error)
                .WithSpan(13, 16, 13, 31)
                .WithArguments("GetArray");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


