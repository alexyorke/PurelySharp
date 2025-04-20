using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicPurityTests
    {
        [Test]
        public async Task TestPureMethod_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes; // Assuming attributes are in this namespace

public class TestClass
{
    [Pure]
    public int GetConstant()
    {
        return 42;
    }
}";

            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
} 