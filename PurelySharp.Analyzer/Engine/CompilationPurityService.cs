using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using PurelySharp.Analyzer.Engine.Analysis;

namespace PurelySharp.Analyzer.Engine
{
    internal sealed class CompilationPurityService
    {
        private readonly ConcurrentDictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> _purityCache = new(SymbolEqualityComparer.Default);

        public CompilationPurityService(Compilation compilation)
        {
            _ = compilation;
            _callGraph = CallGraphBuilder.Build(compilation);
            _compilation = compilation;
        }

        public PurityAnalysisEngine.PurityAnalysisResult GetPurity(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            return _purityCache.GetOrAdd(methodSymbol, m =>
            {
                if (_fixedPoint == null)
                {
                    _fixedPoint = WorklistPuritySolver.Solve(_callGraph, _compilation, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol);
                }
                if (_fixedPoint.TryGetValue(m, out var solved))
                {
                    return solved;
                }
                var engine = new PurityAnalysisEngine();
                return engine.IsConsideredPure(m, semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol);
            });
        }

        private readonly CallGraph _callGraph;
        private readonly Compilation _compilation;
        private volatile System.Collections.Immutable.ImmutableDictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult>? _fixedPoint;
    }
}

