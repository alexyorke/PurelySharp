using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DeepRegressionTests
    {
        [Test]
        public async Task ConstantFalseWhile_IgnoresDeadImpureInvocation()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Run()
    {
        while (false)
        {
            Console.WriteLine(""dead"");
        }

        return 1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstantFalseFor_IgnoresDeadImpureInvocation()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Run()
    {
        for (var i = 0; false; i++)
        {
            Console.WriteLine(i);
        }

        return 1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
