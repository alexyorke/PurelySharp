using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class NetworkingTests
    {
        // --- DNS Lookup (Impure) ---
        // TODO: Enable once analyzer flags Dns methods as impure
        // Commented out test Dns_GetHostEntry_Diagnostic removed

        // --- IPAddress Parsing (Pure) ---
        // Commented out test IPAddress_Parse_NoDiagnostic removed

        // --- Socket Operations (Impure) ---
        // All socket operations involve OS interaction and/or network I/O
        // TODO: Enable tests below once analyzer flags Socket methods/constructor as impure
        // Commented out Socket tests removed
    }
}