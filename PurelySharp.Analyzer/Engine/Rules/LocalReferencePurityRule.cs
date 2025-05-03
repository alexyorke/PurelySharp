using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes local variable references for purity.
    /// Reading a local variable is always pure.
    /// </summary>
    internal class LocalReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.LocalReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ILocalReferenceOperation localReference))
            {
                // Should not happen given ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"WARNING: LocalReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            // Reading a local variable is always pure.
            // Assignment to locals is handled by AssignmentPurityRule.
            PurityAnalysisEngine.LogDebug($"    [LocalRefRule] LocalReference '{localReference.Local.Name}' - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}