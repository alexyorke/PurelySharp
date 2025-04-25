using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of conditional operations (ternary operator ? :).
    /// </summary>
    internal class ConditionalOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Conditional };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IConditionalOperation conditionalOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                PurityAnalysisEngine.LogDebug($"  [CondRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [CondRule] Checking Conditional Operation: {conditionalOperation.Syntax}");

            // Check condition
            var conditionResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.Condition, context);
            if (!conditionResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CondRule] Condition is Impure: {conditionalOperation.Condition.Syntax}");
                return conditionResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] Condition is Pure.");

            // Check WhenTrue branch
            if (conditionalOperation.WhenTrue != null)
            {
                var whenTrueResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.WhenTrue, context);
                if (!whenTrueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue is Impure: {conditionalOperation.WhenTrue.Syntax}");
                    return whenTrueResult;
                }
            }
            else
            {
                // Handle cases where WhenTrue might be null (though unlikely for valid ternary)
                PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue branch is null. Assuming pure.");
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue is Pure.");


            // Check WhenFalse branch
            if (conditionalOperation.WhenFalse != null)
            {
                var whenFalseResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.WhenFalse, context);
                if (!whenFalseResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse is Impure: {conditionalOperation.WhenFalse.Syntax}");
                    return whenFalseResult;
                }
            }
            else
            {
                // Handle cases where WhenFalse might be null (though unlikely for valid ternary)
                PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse branch is null. Assuming pure.");
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse is Pure.");


            // All parts are pure
            PurityAnalysisEngine.LogDebug($"  [CondRule] Conditional Operation is Pure: {conditionalOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}