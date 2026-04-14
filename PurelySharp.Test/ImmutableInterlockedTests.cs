using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ImmutableInterlockedTests
    {
        [Test]
        public async Task ImmutableInterlockedTryAdd_OnField_Diagnostic()
        {
            var test = @"
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    private ImmutableDictionary<string, int> _map = ImmutableDictionary<string, int>.Empty;

    [EnforcePure]
    public void {|PS0002:AddEntry|}()
    {
        ImmutableInterlocked.TryAdd(ref _map, ""a"", 1);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
