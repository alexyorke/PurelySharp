using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of switch statements.
    /// </summary>
    internal class SwitchStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Switch);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ISwitchOperation switchOperation))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: SwitchStatementPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"SwitchStatementPurityRule: Analyzing {switchOperation.Syntax}");

            // Assume the switch statement structure itself is pure.
            // The purity of the value being switched on and the operations within the cases
            // should be determined by other rules during CFG traversal.

            PurityAnalysisEngine.LogDebug($"SwitchStatementPurityRule: Assuming switch statement structure itself is pure for {switchOperation.Syntax}. Case/Value purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}