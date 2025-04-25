using Microsoft.CodeAnalysis;
using PurelySharp.Analyzer.Engine.Rules; // Added for IPurityRule
using System; // Added for Func
using System.Collections.Generic; // Added for IEnumerable
using System.Collections.Immutable; // Added for ImmutableList
using System.Threading; // Added for CancellationToken

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Provides context for purity rule checks within the PurityAnalysisEngine.
    /// </summary>
    internal class PurityAnalysisContext
    {
        // Add CancellationToken property
        public CancellationToken CancellationToken { get; }

        public SemanticModel SemanticModel { get; }
        public INamedTypeSymbol EnforcePureAttributeSymbol { get; }
        public INamedTypeSymbol? PureAttributeSymbol { get; }
        public INamedTypeSymbol? AllowSynchronizationAttributeSymbol { get; }
        public HashSet<IMethodSymbol> VisitedMethods { get; }
        public Dictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> PurityCache { get; }
        public IMethodSymbol ContainingMethodSymbol { get; }
        // Added PurityRules property
        public ImmutableList<IPurityRule> PurityRules { get; }

        public PurityAnalysisContext(
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? pureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visitedMethods,
            Dictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> purityCache,
            IMethodSymbol containingMethodSymbol,
            ImmutableList<IPurityRule> purityRules,
            CancellationToken cancellationToken)
        {
            SemanticModel = semanticModel;
            EnforcePureAttributeSymbol = enforcePureAttributeSymbol;
            PureAttributeSymbol = pureAttributeSymbol;
            AllowSynchronizationAttributeSymbol = allowSynchronizationAttributeSymbol;
            VisitedMethods = visitedMethods;
            PurityCache = purityCache;
            ContainingMethodSymbol = containingMethodSymbol;
            PurityRules = purityRules;
            CancellationToken = cancellationToken;
        }
    }
}