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
            // Treated as pure as it usually returns a stable value read at startup
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Environment.ProcessorCount;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}