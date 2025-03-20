using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Core
{
    /// <summary>
    /// Context for purity analysis, containing configuration and state.
    /// </summary>
    public class PurityContext
    {
        /// <summary>
        /// The required purity level for analysis.
        /// </summary>
        public PurityLevel RequiredLevel { get; }

        /// <summary>
        /// The current call stack during analysis.
        /// </summary>
        public ImmutableStack<IMethodSymbol> CallStack { get; }

        /// <summary>
        /// Tracks state mutations during analysis.
        /// </summary>
        public IDictionary<ISymbol, MutabilityInfo> StateTracker { get; }

        /// <summary>
        /// Configuration options for the analysis.
        /// </summary>
        public PurityAnalyzerConfiguration Configuration { get; }

        public PurityContext(
            PurityLevel requiredLevel,
            ImmutableStack<IMethodSymbol>? callStack = null,
            IDictionary<ISymbol, MutabilityInfo>? stateTracker = null,
            PurityAnalyzerConfiguration? configuration = null)
        {
            RequiredLevel = requiredLevel;
            CallStack = callStack ?? ImmutableStack<IMethodSymbol>.Empty;
            StateTracker = stateTracker ?? new Dictionary<ISymbol, MutabilityInfo>(SymbolEqualityComparer.Default);
            Configuration = configuration ?? new PurityAnalyzerConfiguration();
        }

        /// <summary>
        /// Creates a new context with an updated call stack.
        /// </summary>
        public PurityContext WithMethod(IMethodSymbol method)
        {
            return new PurityContext(
                RequiredLevel,
                CallStack.Push(method),
                StateTracker,
                Configuration);
        }
    }
}