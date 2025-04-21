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
    public class NonDeterministicOperationsTests
    {
        [Test]
        public async Task ImpureMethodWithRandomOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return new Random().Next();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


