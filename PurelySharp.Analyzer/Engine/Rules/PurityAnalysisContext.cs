﻿using Microsoft.CodeAnalysis;
using PurelySharp.Analyzer.Engine.Rules;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class PurityAnalysisContext
    {
        public CancellationToken CancellationToken { get; }
        public SemanticModel SemanticModel { get; }
        public INamedTypeSymbol EnforcePureAttributeSymbol { get; }
        public INamedTypeSymbol? PureAttributeSymbol { get; }
        public INamedTypeSymbol? AllowSynchronizationAttributeSymbol { get; }
        public HashSet<IMethodSymbol> VisitedMethods { get; }
        public Dictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> PurityCache { get; }
        public IMethodSymbol ContainingMethodSymbol { get; }
        public ImmutableList<IPurityRule> PurityRules { get; }
        public CompilationPurityService? PurityService { get; }

        public PurityAnalysisContext(
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? pureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visitedMethods,
            Dictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> purityCache,
            IMethodSymbol containingMethodSymbol,
            ImmutableList<IPurityRule> purityRules,
            CancellationToken cancellationToken,
            CompilationPurityService? purityService)
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
            PurityService = purityService;
        }
    }
}