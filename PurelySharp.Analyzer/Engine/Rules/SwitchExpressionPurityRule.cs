using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp; // Added for SyntaxKind
using Microsoft.CodeAnalysis.CSharp.Syntax; // Added for Increment/DecrementExpressionSyntax

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of switch expressions.
    /// </summary>
    internal class SwitchExpressionPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.SwitchExpression);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ISwitchExpressionOperation switchExpression))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: SwitchExpressionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [SwitchExprRule] Checking Switch Expression: {switchExpression.Syntax}");

            // 1. Check the expression being switched on
            var valueResult = PurityAnalysisEngine.CheckSingleOperation(switchExpression.Value, context, currentState);
            if (!valueResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [SwitchExprRule] Value expression is Impure: {switchExpression.Value.Syntax}");
                return valueResult;
            }

            // 2. Check all switch expression arms
            foreach (var arm in switchExpression.Arms)
            {
                PurityAnalysisEngine.LogDebug($"    [SwitchExprRule] Checking Arm: {arm.Syntax}");

                // Check pattern (if any)
                if (arm.Pattern != null)
                {
                    PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Checking Pattern: {arm.Pattern.Syntax} ({arm.Pattern.Kind})");
                    var patternResult = PurityAnalysisEngine.CheckSingleOperation(arm.Pattern, context, currentState);
                    if (!patternResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Pattern is IMPURE. Switch expression is Impure.");
                        return patternResult;
                    }
                }

                // Check guard (when clause, if any)
                if (arm.Guard != null)
                {
                    PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Checking Guard: {arm.Guard.Syntax} ({arm.Guard.Kind})");
                    var guardResult = PurityAnalysisEngine.CheckSingleOperation(arm.Guard, context, currentState);
                    if (!guardResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Guard is IMPURE. Switch expression is Impure.");
                        return guardResult;
                    }
                }

                // Check the expression value of the arm
                PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Checking Value: {arm.Value.Syntax} ({arm.Value.Kind})");
                var armValueResult = PurityAnalysisEngine.CheckSingleOperation(arm.Value, context, currentState);
                if (!armValueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"      [SwitchExprRule.Arm] Value is IMPURE. Switch expression is Impure.");
                    return armValueResult;
                }
            }

            PurityAnalysisEngine.LogDebug($"SwitchExpressionPurityRule: All parts checked and pure (or impurity overridden) for {switchExpression.Syntax}.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}