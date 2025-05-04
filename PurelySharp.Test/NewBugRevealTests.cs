using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BugRevealTests
    {
        [Test]
        public async Task RecordStruct_PropertyAssignment_ShouldFlagPS0002()
        {
            var test = @"#nullable enable
using PurelySharp.Attributes;

public record struct Counter
{
    public int Value { get; set; }

    [EnforcePure]
    public Counter {|PS0002:Increment|}()
    {
        // Property assignment – mutates state, hence impure
        Value += 1;
        return this;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}