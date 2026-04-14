using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionExpressionSpreadTests
    {
        [Test]
        public async Task ImmutableArraySpread_NoDiagnostic()
        {
            var test = @"
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ImmutableArray<int> Extend(ImmutableArray<int> values)
    {
        return [.. values, 42];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImmutableArraySpread_ImpureOperand_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    private static ImmutableArray<int> GetValues()
    {
        Console.WriteLine(""side effect"");
        return ImmutableArray<int>.Empty;
    }

    [EnforcePure]
    public ImmutableArray<int> {|PS0002:Extend|}()
    {
        return [.. GetValues(), 42];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
