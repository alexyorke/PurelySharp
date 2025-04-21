using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DiagnosticsTests
    {
        // --- Stopwatch Tests ---

        // TODO: Enable once analyzer recognizes Stopwatch methods as impure
        // Commented out tests removed

        // --- Process Tests ---
        // TODO: Enable once analyzer recognizes Process methods as impure
        // Commented out tests removed
    }
}