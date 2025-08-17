using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ConditionalOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Conditional);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IConditionalOperation conditionalOperation))
            {

                PurityAnalysisEngine.LogDebug($"  [CondRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [CondRule] Checking Conditional Operation: {conditionalOperation.Syntax}");


            var conditionResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.Condition, context, currentState);
            if (!conditionResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [CondRule] Condition is Impure: {conditionalOperation.Condition.Syntax}");
                return conditionResult;
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] Condition is Pure.");


            if (conditionalOperation.WhenTrue != null)
            {
                var whenTrueResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.WhenTrue, context, currentState);
                if (!whenTrueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue is Impure: {conditionalOperation.WhenTrue.Syntax}");
                    return whenTrueResult;
                }
            }
            else
            {

                PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue branch is null. Assuming pure.");
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] WhenTrue is Pure.");



            if (conditionalOperation.WhenFalse != null)
            {
                var whenFalseResult = PurityAnalysisEngine.CheckSingleOperation(conditionalOperation.WhenFalse, context, currentState);
                if (!whenFalseResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse is Impure: {conditionalOperation.WhenFalse.Syntax}");
                    return whenFalseResult;
                }
            }
            else
            {

                PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse branch is null. Assuming pure.");
            }
            PurityAnalysisEngine.LogDebug($"    [CondRule] WhenFalse is Pure.");



            PurityAnalysisEngine.LogDebug($"  [CondRule] Conditional Operation is Pure: {conditionalOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}