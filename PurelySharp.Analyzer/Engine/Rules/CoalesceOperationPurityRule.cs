using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class CoalesceOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Coalesce);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ICoalesceOperation coalesceOperation))
            {

                PurityAnalysisEngine.LogDebug($"  [CoalesceRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [CoalesceRule] Checking Coalesce Operation: {coalesceOperation.Syntax}");








            var leftResult = PurityAnalysisEngine.CheckSingleOperation(coalesceOperation.Value, context, currentState);
            if (!leftResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Left side is Impure: {coalesceOperation.Value.Syntax}");
                return leftResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Left side is Pure.");


            var rightResult = PurityAnalysisEngine.CheckSingleOperation(coalesceOperation.WhenNull, context, currentState);
            if (!rightResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Right side (WhenNull) is Impure: {coalesceOperation.WhenNull.Syntax}");
                return rightResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Right side (WhenNull) is Pure.");


            PurityAnalysisEngine.LogDebug($"  [CoalesceRule] Coalesce Operation is Pure: {coalesceOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}