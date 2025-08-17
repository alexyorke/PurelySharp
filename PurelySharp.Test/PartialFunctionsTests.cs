using NUnit.Framework;
using System;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    [Category("Partial Functions")]
    public class PartialFunctionsTests
    {


        [Test]
        public async Task TestPartialFunction_ThrowsException_NoDiagnostic()
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
    // Add [EnforcePure] to trigger analysis of this method
    [EnforcePure]
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
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }


    }
}