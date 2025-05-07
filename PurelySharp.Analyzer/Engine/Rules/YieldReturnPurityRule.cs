using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles yield return statements.
    /// The purity of the yield return operation itself depends on the purity of the yielded value.
    /// The overall iterator method's purity is determined by CFG analysis of the generated state machine.
    /// </summary>
    internal class YieldReturnPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.YieldReturn);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IReturnOperation yieldReturnOperation))
            {
                // Should not happen given ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Unexpected operation type. Assuming impure.");
                return PurityAnalysisEngine.ImpureResult(operation.Syntax);
            }

            // Check the expression being yielded
            if (yieldReturnOperation.ReturnedValue != null)
            {
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Checking yielded value: {yieldReturnOperation.ReturnedValue.Syntax} ({yieldReturnOperation.ReturnedValue.Kind})");
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(yieldReturnOperation.ReturnedValue, context, currentState);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Yielded value is IMPURE. Yield return operation is Impure.");
                    return valueResult; // Propagate the impurity result from the value
                }
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Yielded value is PURE.");
            }
            else
            {
                // yield return default; or similar - this is pure if there's no value.
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Yield return has no value. Operation is Pure.");
            }

            // If the yielded value is pure (or no value), the yield return operation itself is considered pure.
            // The overall method purity depends on other operations in the iterator's state machine.
            PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Yield return operation itself (excluding other method code) is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}