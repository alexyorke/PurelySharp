using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class LocalReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.LocalReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ILocalReferenceOperation localReference))
            {

                PurityAnalysisEngine.LogDebug($"WARNING: LocalReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }



            PurityAnalysisEngine.LogDebug($"    [LocalRefRule] LocalReference '{localReference.Local.Name}' - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}