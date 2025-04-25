using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of switch expressions.
    /// </summary>
    internal class SwitchExpressionPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.SwitchExpression);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ISwitchExpressionOperation switchExpression))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: SwitchExpressionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"SwitchExpressionPurityRule: Analyzing {switchExpression.Syntax}");

            // Check the value being switched on
            PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Checking ValueToMatch ({switchExpression.Value?.Kind})");
            if (switchExpression.Value != null)
            {
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(switchExpression.Value, context);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: ValueToMatch is IMPURE.");
                    return valueResult;
                }
                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: ValueToMatch is Pure.");
            }

            // Check each arm (Pattern, WhenClause, Value)
            foreach (var arm in switchExpression.Arms)
            {
                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Checking Arm Pattern ({arm.Pattern?.Kind})");
                if (arm.Pattern != null)
                {
                    // Note: Pattern matching operations themselves are generally pure,
                    // but we check anyway in case a complex pattern operation exists.
                    var patternResult = PurityAnalysisEngine.CheckSingleOperation(arm.Pattern, context);
                    if (!patternResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm Pattern is IMPURE.");
                        return patternResult;
                    }
                }

                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Checking Arm Value ({arm.Value?.Kind})");
                if (arm.Value != null) // Value can be null if arm throws an exception
                {
                    var armValueResult = PurityAnalysisEngine.CheckSingleOperation(arm.Value, context);
                    if (!armValueResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm Value is IMPURE.");
                        return armValueResult;
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm Value is null (e.g., throws). Assuming handled by other operations.");
                }
                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm processed and appears Pure.");
            }

            PurityAnalysisEngine.LogDebug($"SwitchExpressionPurityRule: All parts checked and pure for {switchExpression.Syntax}.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}