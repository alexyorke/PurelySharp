using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class CoalesceOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Coalesce };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ICoalesceOperation coalesceOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                PurityAnalysisEngine.LogDebug($"  [CoalesceRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [CoalesceRule] Checking Coalesce Operation: {coalesceOperation.Syntax}");

            // The ICoalesceOperation has:
            // 1. Value: The expression evaluated if the WhenNull expression IS NOT null. (Right operand in C# `??`)
            // 2. WhenNull: The expression evaluated first. If it IS null, its result is discarded,
            //              and the Value expression is evaluated. (Left operand in C# `??`)
            // Note the somewhat counter-intuitive naming.

            // Check the left operand (WhenNull)
            var whenNullResult = PurityAnalysisEngine.CheckSingleOperation(coalesceOperation.WhenNull, context);
            if (!whenNullResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Left operand (WhenNull) is Impure: {coalesceOperation.WhenNull.Syntax}");
                return whenNullResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Left operand (WhenNull) is Pure.");

            // Check the right operand (Value)
            var valueResult = PurityAnalysisEngine.CheckSingleOperation(coalesceOperation.Value, context);
            if (!valueResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Right operand (Value) is Impure: {coalesceOperation.Value.Syntax}");
                return valueResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CoalesceRule] Right operand (Value) is Pure.");


            // Both operands are pure
            PurityAnalysisEngine.LogDebug($"  [CoalesceRule] Coalesce Operation is Pure: {coalesceOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}