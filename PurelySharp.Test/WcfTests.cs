using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
// using System.ServiceModel; // Requires adding WCF packages
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class WcfTests
    {
        // Note: These tests require adding WCF packages (System.ServiceModel.Primitives, etc.)
        // and potentially more setup (defining service/data contracts).
        // They serve as placeholders for future analysis if WCF support is added.
        // All typical WCF operations (client calls, service implementations, hosting) are impure.

        // TODO: Add real tests if WCF analysis becomes a priority.

        // Commented out tests removed as per instruction
    }
}