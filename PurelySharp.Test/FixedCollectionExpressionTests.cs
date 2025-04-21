using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
using PurelySharp.Attributes;



public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] {|PS0002:GetNumbers|}()
    {
        // Using new[] array creation expression
        return new[] { 1, 2, 3, 4, 5 };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] {|PS0002:GetArray|}()
    {
        // Using collection expression syntax
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


