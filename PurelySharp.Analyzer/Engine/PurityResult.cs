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
        // public ImmutableArray<Location> ImpurityLocations { get; }

        // TODO: Add constructor
        // public PurityResult(PurityLevel level, ImmutableArray<Location> locations = default)
        // {
        //    Level = level;
        //    ImpurityLocations = locations.IsDefault ? ImmutableArray<Location>.Empty : locations;
        // }

        // Consider adding factory methods, e.g., PurityResult.CreatePure(), PurityResult.CreateImpure(...)
    }
} 