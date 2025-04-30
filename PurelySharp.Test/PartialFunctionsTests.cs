using NUnit.Framework;
using System;
using System.Threading.Tasks;
using PurelySharp.Analyzer; // Assuming this namespace contains the analyzer
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // For [EnforcePure]

namespace PurelySharp.Test
{
    [TestFixture]
    [Category("Partial Functions")] // Apply category at the class level
    public class PartialFunctionsTests
    {
        // No longer need the helper method directly in the test class

        [Test]
        public async Task TestPartialFunction_ThrowsException_ShouldFlagPS0002()
        {
            var testCode = @"
#nullable enable
using System; // Added for ArgumentNullException
using PurelySharp.Attributes; // Added for [EnforcePure]

public class TestClass
{
    [EnforcePure]
    // Throwing an exception is an impure operation (side effect)
    public int IdentityOrThrowIfNull(int? input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        return input.Value;
    }

    // Example usage (not strictly necessary for analyzer check, but good context)
    public void UseMethod()
    {
        try
        {
           var result = IdentityOrThrowIfNull(5);
           var result2 = IdentityOrThrowIfNull(null); // This line would throw at runtime
        }
        catch (ArgumentNullException)
        {
           // Expected for null input
        }
    }
}
"; // End of verbatim string
            // Verify that the analyzer flags the method definition due to the throw statement.
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                           .WithSpan(10, 16, 10, 37) // Updated Span to method identifier
                           .WithArguments("IdentityOrThrowIfNull");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
        }

        // Removed the previous runtime tests as they are superseded by the analyzer test.
    }
}