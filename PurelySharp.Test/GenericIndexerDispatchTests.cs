using NUnit.Framework;
using PurelySharp.Analyzer;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class GenericIndexerDispatchTests
    {
        [Test]
        public async Task DictionaryIndexerWithUnresolvedGenericKey_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass<TKey, TValue>
{
    [EnforcePure]
    public TValue {|PS0002:TestMethod|}(Dictionary<TKey, TValue> values, TKey key)
    {
        return values[key];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SortedDictionaryIndexerWithUnresolvedGenericKey_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass<TKey, TValue>
{
    [EnforcePure]
    public TValue {|PS0002:TestMethod|}(SortedDictionary<TKey, TValue> values, TKey key)
    {
        return values[key];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
