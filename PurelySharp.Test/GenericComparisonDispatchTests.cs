using NUnit.Framework;
using PurelySharp.Analyzer;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class GenericComparisonDispatchTests
    {
        [Test]
        public async Task SortedSetContainsWithUnresolvedGenericElement_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass<T>
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(SortedSet<T> values, T value)
    {
        return values.Contains(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqOrderByWithUnresolvedGenericElement_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass<T>
{
    [EnforcePure]
    public IOrderedEnumerable<T> {|PS0002:TestMethod|}(IEnumerable<T> values)
    {
        return values.OrderBy(static value => value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
