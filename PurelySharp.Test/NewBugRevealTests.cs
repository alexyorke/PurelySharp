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
    public int Value { get; set; } // Line 6 - Target for PS0004 get/set

    [EnforcePure]
    public Counter Increment() // Line 9 - Target for PS0002
    {
        // Property assignment – mutates state, hence impure
        Value += 1;
        return this;
    }
}";


            var expectedIncrement = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                           .WithSpan(9, 20, 9, 29)
                                           .WithArguments("Increment");

            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(6, 16, 6, 21)
                                          .WithArguments("get_Value");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedIncrement, expectedGetValue);
        }
    }
}