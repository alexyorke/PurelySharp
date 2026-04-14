using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ImmutableStackTests
    {
        [Test]
        public async Task ImmutableStackPush_NoDiagnostic()
        {
            var test = @"
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ImmutableStack<int> PushValue(ImmutableStack<int> stack, int value)
    {
        return stack.Push(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
