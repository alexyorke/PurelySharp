using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionMaterializationTests
    {
        [Test]
        public async Task ListFindAll_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public List<int> {|PS0002:TestMethod|}(List<int> values)
    {
        return values.FindAll(static value => value > 0);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
