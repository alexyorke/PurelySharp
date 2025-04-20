using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
// using PurelySharp.Analyzer.Configuration; // TODO: Uncomment when defined
// using PurelySharp.Analyzer.Engine;       // TODO: Uncomment when defined

namespace PurelySharp.Analyzer
{
    /// <summary>
    /// Holds immutable state shared across the analysis of a single compilation.
    /// </summary>
    internal class CompilationState
    {
        // public PurityAnalysisEngine PurityAnalysisEngine { get; }
        // public AnalyzerConfiguration AnalyzerConfiguration { get; }

        // TODO: Use SymbolEqualityComparer.Default for ISymbol keys
        // public ImmutableDictionary<ISymbol, PurityResult> PurityCache { get; } = ImmutableDictionary<ISymbol, PurityResult>.Empty;
        // public ImmutableHashSet<ISymbol> RecursionGuard { get; } = ImmutableHashSet<ISymbol>.Empty;

        // TODO: Add constructor to initialize properties
        // public CompilationState(PurityAnalysisEngine engine, AnalyzerConfiguration config)
        // {
        //     PurityAnalysisEngine = engine;
        //     AnalyzerConfiguration = config;
        //     // Initialize cache/guard as needed
        // }

        // TODO: Add methods for updating cache/guard immutably if necessary, e.g.:
        // public CompilationState WithPurityResult(ISymbol symbol, PurityResult result)
        // {
        //     // Return new CompilationState with updated PurityCache
        // }
        // public CompilationState EnterAnalysis(ISymbol symbol)
        // {
        //    // Return new CompilationState with updated RecursionGuard
        // }
        // public CompilationState ExitAnalysis(ISymbol symbol)
        // {
        //     // Return new CompilationState with updated RecursionGuard
        // }
    }
} 