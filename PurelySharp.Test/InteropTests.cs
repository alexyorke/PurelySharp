using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class InteropTests
    {
        // --- P/Invoke Tests (Impure) ---
        // P/Invoke calls are inherently impure as they interact with unmanaged code/OS.
        // TODO: Enable tests once analyzer can detect P/Invoke calls.
        // Commented out test PInvoke_MessageBox_Diagnostic removed

        // --- COM Interop Tests (Impure) ---
        // COM Interop involves interacting with external COM components, which is impure.
        // TODO: Add tests for COM Interop usage once analyzer support is considered.
        // (Requires setting up COM references/registration, potentially complex for unit tests).
        // Commented out test ComInterop_CreateObject_Diagnostic removed
    }
}