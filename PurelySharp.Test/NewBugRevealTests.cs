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
            // The original {|PS0002:Increment|} syntax is a shorthand for one expected diagnostic.
            // We now explicitly define all expected diagnostics.
            var expectedIncrement = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId) // PS0002
                                           .WithSpan(9, 20, 9, 29) // Span of "Increment"
                                           .WithArguments("Increment");

            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId) // PS0004
                                          .WithSpan(6, 16, 6, 21) // Span of "Value" property name
                                          .WithArguments("get_Value");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedIncrement, expectedGetValue);
        }
    }
}