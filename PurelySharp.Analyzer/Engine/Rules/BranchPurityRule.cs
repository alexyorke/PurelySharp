using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes branch operations (conditional/unconditional jumps) for purity.
    /// Branching itself is a pure control-flow operation.
    /// </summary>
    internal class BranchPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Branch);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // Branching is pure control flow.
            PurityAnalysisEngine.LogDebug($"    [BranchRule] Branch operation ({operation.Syntax}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}