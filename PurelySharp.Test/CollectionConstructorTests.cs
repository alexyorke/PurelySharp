using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionConstructorTests
    {
        [Test]
        public async Task ListConstructor_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public List<int> {|PS0002:TestMethod|}()
    {
        return new List<int>();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
