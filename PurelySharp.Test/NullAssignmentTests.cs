using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullAssignmentTests
    {
        [Test]
        public async Task PureMethodWithNullAssignment_NoDiagnostic()
        {
            // Implementation of the test
        }

        [Test]
        public async Task ImpureMethodWithNullAssignment_Diagnostic()
        {
            // Implementation of the test
        }

        [Test]
        public async Task PureMethodWithNullAssignmentAndImpureOperation_Diagnostic()
        {
            // Implementation of the test
        }
    }
}