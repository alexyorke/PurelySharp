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
    public class ApplicationModelTests
    {
        // --- Blazor Component Methods (Usually Impure) ---
        // Blazor lifecycle methods (OnInitializedAsync, etc.) and event handlers (@onclick)
        // typically interact with state, UI, services, or JS interop, making them impure.
        // TODO: Add tests for Blazor component methods once analysis of UI frameworks is feasible.

        // Removed Blazor placeholder tests

        // --- Worker Service ExecuteAsync (Usually Impure) ---
        // The main loop of a Worker Service typically involves long-running tasks,
        // I/O, interaction with external services, etc., making it inherently impure.
        // TODO: Add tests for Worker Service ExecuteAsync once analysis of hosted services is feasible.

        // Removed Worker Service placeholder test
    }
}