using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using PurelySharp.Analyzer.Engine.Analysis;

namespace PurelySharp.Analyzer.Engine
{
    internal sealed class CompilationPurityService
    {
        private readonly ConcurrentDictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> _purityCache = new(SymbolEqualityComparer.Default);
        private readonly object _fixedPointLock = new();

        public CompilationPurityService(Compilation compilation)
        {
            _compilation = compilation;
        }

        public PurityAnalysisEngine.PurityAnalysisResult GetPurity(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            EnsureFixedPoint(enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol);

            return _purityCache.GetOrAdd(methodSymbol, m =>
            {
                if (_fixedPoint!.TryGetValue(m, out var solved))
                {
                    return solved;
                }
                var engine = new PurityAnalysisEngine(this);
                return engine.IsConsideredPure(m, semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol);
            });
        }

        private void EnsureFixedPoint(
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            if (_fixedPoint != null)
            {
                return;
            }

            lock (_fixedPointLock)
            {
                if (_fixedPoint != null)
                {
                    return;
                }

                _callGraph ??= CallGraphBuilder.Build(_compilation);
                _fixedPoint = WorklistPuritySolver.Solve(
                    _callGraph,
                    _compilation,
                    enforcePureAttributeSymbol,
                    allowSynchronizationAttributeSymbol);
            }
        }

        private CallGraph? _callGraph;
        private readonly Compilation _compilation;
        private volatile System.Collections.Immutable.ImmutableDictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult>? _fixedPoint;
    }
}
