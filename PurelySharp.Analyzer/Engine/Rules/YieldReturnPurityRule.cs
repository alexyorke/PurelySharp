using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles yield return statements.
    /// Considered impure because it involves hidden state machine manipulation.
    /// </summary>
    internal class YieldReturnPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.YieldReturn);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IReturnOperation yieldReturnOperation))
            {
                // Yield return implies state machine manipulation, treat as impure.
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] YieldReturn operation ({operation.Syntax}) - Impure");
                // Even if the returned expression itself is pure, the yield mechanism isn't.
                return PurityAnalysisEngine.ImpureResult(operation.Syntax);
            }

            // Check the expression being yielded
            if (yieldReturnOperation.ReturnedValue != null)
            {
                PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Checking yielded value: {yieldReturnOperation.ReturnedValue.Syntax} ({yieldReturnOperation.ReturnedValue.Kind})");
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(yieldReturnOperation.ReturnedValue, context, currentState);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] Yielded value is IMPURE. Yield return is Impure.");
                    return valueResult;
                }
            }

            return PurityAnalysisEngine.ImpureResult(operation.Syntax);
        }
    }
}