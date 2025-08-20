using NUnit.Framework;
using System;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ThrowExpressionTests
    {
        [Test]
        public async Task MethodWithThrowExpression_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public int TestMethod(int value)
        {
            return value >= 0 ? value : throw new ArgumentException(""Invalid value"");
        }
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}