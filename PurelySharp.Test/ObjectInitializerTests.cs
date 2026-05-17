using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ObjectInitializerTests
    {
        [Test]
        public async Task ObjectInitializerWithImpureSetter_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class Target
{
    public int Value
    {
        set { Console.WriteLine(value); }
    }
}

public class TestClass
{
    [EnforcePure]
    public Target {|PS0002:Create|}()
    {
        return new Target { Value = 1 };
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}
