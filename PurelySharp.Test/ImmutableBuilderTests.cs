using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ImmutableBuilderTests
    {
        [Test]
        public async Task ImmutableListBuilderAdd_OnParameter_Diagnostic()
        {
            var test = @"
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:MutateBuilder|}(ImmutableList<int>.Builder builder)
    {
        builder.Add(1);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
