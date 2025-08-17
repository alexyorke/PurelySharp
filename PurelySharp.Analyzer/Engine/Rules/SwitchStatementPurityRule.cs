using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class SwitchStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Switch);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ISwitchOperation switchOperation))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: SwitchStatementPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"SwitchStatementPurityRule: Analyzing {switchOperation.Syntax}");





            PurityAnalysisEngine.LogDebug($"SwitchStatementPurityRule: Assuming switch statement structure itself is pure for {switchOperation.Syntax}. Case/Value purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}