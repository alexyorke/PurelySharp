using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ForeachLoopTests
    {
        [Test]
        public async Task ForeachImpureCollectionExpression_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        foreach (var value in GetValues())
        {
        }
    }

    private IEnumerable<int> GetValues()
    {
        Console.WriteLine(""loading"");
        return Array.Empty<int>();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
