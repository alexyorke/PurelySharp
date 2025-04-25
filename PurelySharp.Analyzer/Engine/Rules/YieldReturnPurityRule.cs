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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // Yield return implies state machine manipulation, treat as impure.
            PurityAnalysisEngine.LogDebug($"    [YieldReturnRule] YieldReturn operation ({operation.Syntax}) - Impure");
            // Even if the returned expression itself is pure, the yield mechanism isn't.
            return PurityAnalysisEngine.ImpureResult(operation.Syntax);

            // Future enhancement: Could potentially check the returned expression's purity
            // if needed for more granular analysis, but the yield itself is the primary concern.
            // if (operation is IYieldReturnOperation yieldOp && yieldOp.ReturnedValue != null)
            // {
            //     var valueResult = PurityAnalysisEngine.CheckSingleOperation(yieldOp.ReturnedValue, context);
            //     if (!valueResult.IsPure) return valueResult;
            // }
            // return PurityAnalysisEngine.ImpureResult(operation.Syntax);
        }
    }
}