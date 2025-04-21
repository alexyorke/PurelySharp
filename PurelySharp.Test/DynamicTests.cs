using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class DynamicTests
    {
        // --- Dynamic Operations (Impure) ---
        // Operations on `dynamic` objects involve runtime binding (DLR) which interacts
        // with runtime state and hides the exact operation, making static purity analysis difficult.
        // Therefore, dynamic operations are conservatively treated as impure.

        // TODO: Enable tests once analyzer flags dynamic operations as impure.
        // Commented out tests removed
    }
}