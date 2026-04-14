using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionArrayTests
    {
        [Test]
        public async Task ListToArray_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(List<int> values)
    {
        return values.ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
