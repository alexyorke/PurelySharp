using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Represents the outcome of a purity analysis.
    /// </summary>
    internal enum PurityLevel
    {
        Pure,
        ConditionallyPure, // e.g., depends on generic type arguments
        Impure
    }

    internal class PurityResult
    {
        public PurityLevel Level { get; }

        // TODO: Add locations of impurities if Level is Impure
        // TODO: Add constructor
        // Consider adding factory methods, e.g., PurityResult.CreatePure(), PurityResult.CreateImpure(...)
    }
} 