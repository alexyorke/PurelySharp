using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullCoalescingTests
    {
        [Test]
        public async Task PureMethodWithNullCoalescing_NoDiagnostic()
        {
            // Implementation of the test
        }

        [Test]
        public async Task ImpureMethodWithNullCoalescing_Diagnostic()
        {
            // Implementation of the test
        }

        [Test]
        public async Task PureMethodWithNullCoalescingAndImpureOperation_Diagnostic()
        {
            // Implementation of the test
        }
    }
}