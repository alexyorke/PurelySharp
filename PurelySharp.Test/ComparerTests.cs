using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ComparerTests
    {
        [Test]
        public async Task EqualityComparerDefault_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public EqualityComparer<int> {|PS0002:TestMethod|}()
    {
        return EqualityComparer<int>.Default;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ComparerDefault_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Comparer<int> {|PS0002:TestMethod|}()
    {
        return Comparer<int>.Default;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
