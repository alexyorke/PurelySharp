using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionViewTests
    {
        [Test]
        public async Task DictionaryKeys_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Dictionary<int, string>.KeyCollection {|PS0002:TestMethod|}(Dictionary<int, string> values)
    {
        return values.Keys;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DictionaryValues_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Dictionary<int, string>.ValueCollection {|PS0002:TestMethod|}(Dictionary<int, string> values)
    {
        return values.Values;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
