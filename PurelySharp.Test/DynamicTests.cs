using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class DynamicTests
    {
        [Test]
        public async Task DynamicUnaryOperation_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static readonly dynamic DynamicValue = 10;

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return -DynamicValue;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
