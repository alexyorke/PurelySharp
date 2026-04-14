using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ObjectEqualsDispatchTests
    {
        [Test]
        public async Task ObjectEqualsOnObjectParameter_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(object left, object right)
    {
        return left.Equals(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
