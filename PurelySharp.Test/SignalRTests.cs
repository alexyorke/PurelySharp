using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
// using Microsoft.AspNetCore.SignalR.Client; // Requires SignalR packages
// using Microsoft.AspNetCore.SignalR;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class SignalRTests
    {
        // Note: These tests require adding SignalR packages (Client and/or Core).
        // They serve as placeholders for future analysis if SignalR support is added.
        // Typical SignalR hub methods, client invocations, and message handlers are impure
        // due to network I/O and potential state manipulation.

        // TODO: Add real tests if SignalR analysis becomes a priority.

        // Commented out tests removed as per instruction
    }
}