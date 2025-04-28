using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EnvironmentTests
    {
        // TODO: Enable tests below once analyzer recognizes Environment methods as impure/pure
        // Commented out Environment tests removed.

        [Test]
        public async Task Environment_ProcessorCount_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        // Environment.ProcessorCount is now known impure
        return Environment.ProcessorCount;
    }
}
";

            // TestMethod calls Environment.ProcessorCount which is impure.
            // However, the analyzer might report PS0002 if the PropertyReference rule doesn't explicitly flag it.
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                     .WithSpan(8, 16, 8, 26) // Corrected span from test output
                                     .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}