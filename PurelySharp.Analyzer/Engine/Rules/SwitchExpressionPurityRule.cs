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

            // --- MODIFICATION START --- 
            PurityAnalysisEngine.PurityAnalysisResult firstNonOverriddenImpurity = PurityAnalysisEngine.PurityAnalysisResult.Pure; // Track first "real" impurity

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
                        // Update firstNonOverriddenImpurity if it's still pure
                        if (firstNonOverriddenImpurity.IsPure) firstNonOverriddenImpurity = patternResult;
                        // Continue checking other arms in case they have other impurities, but keep the first one found.
                    }
                }

                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Checking Arm Value ({arm.Value?.Kind})");
                if (arm.Value != null) // Value can be null if arm throws an exception
                {
                    var armValueResult = PurityAnalysisEngine.CheckSingleOperation(arm.Value, context);
                    if (!armValueResult.IsPure)
                    {
                        // --- SPECIAL CASE CHECK --- 
                        bool impurityOverridden = false;
                        if (armValueResult.ImpureSyntaxNode is PrefixUnaryExpressionSyntax prefixUnary &&
                            (prefixUnary.Kind() == SyntaxKind.PreIncrementExpression || prefixUnary.Kind() == SyntaxKind.PreDecrementExpression))
                        {
                            var operandOp = context.SemanticModel.GetOperation(prefixUnary.Operand, context.CancellationToken);
                            if (operandOp is IFieldReferenceOperation fieldRef &&
                                fieldRef.Instance is IInstanceReferenceOperation instanceRef &&
                                instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                                fieldRef.Field != null && !fieldRef.Field.IsStatic)
                            {
                                PurityAnalysisEngine.LogDebug($"    [SwitchExprRule] OVERRIDE: Impurity from ++/-- on 'this.{fieldRef.Field.Name}' in arm value ignored.");
                                impurityOverridden = true;
                            }
                        }
                        else if (armValueResult.ImpureSyntaxNode is PostfixUnaryExpressionSyntax postfixUnary &&
                                 (postfixUnary.Kind() == SyntaxKind.PostIncrementExpression || postfixUnary.Kind() == SyntaxKind.PostDecrementExpression))
                        {
                            var operandOp = context.SemanticModel.GetOperation(postfixUnary.Operand, context.CancellationToken);
                            if (operandOp is IFieldReferenceOperation fieldRef &&
                               fieldRef.Instance is IInstanceReferenceOperation instanceRef &&
                               instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                               fieldRef.Field != null && !fieldRef.Field.IsStatic)
                            {
                                PurityAnalysisEngine.LogDebug($"    [SwitchExprRule] OVERRIDE: Impurity from ++/-- on 'this.{fieldRef.Field.Name}' in arm value ignored.");
                                impurityOverridden = true;
                            }
                        }
                        // --- END SPECIAL CASE CHECK ---

                        if (!impurityOverridden)
                        {
                            PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm Value is IMPURE (Non-overridden).");
                            // Update firstNonOverriddenImpurity if it's still pure
                            if (firstNonOverriddenImpurity.IsPure) firstNonOverriddenImpurity = armValueResult;
                            // Continue checking other arms
                        }
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm Value is null (e.g., throws). Assuming handled by other operations.");
                }
                PurityAnalysisEngine.LogDebug($" SwitchExpressionPurityRule: Arm processed and appears Pure.");
            }

            // Return the first non-overridden impurity found, or Pure if none were found.
            if (!firstNonOverriddenImpurity.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"SwitchExpressionPurityRule: Finished checking arms. Returning first non-overridden impure result.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"SwitchExpressionPurityRule: All parts checked and pure (or impurity overridden) for {switchExpression.Syntax}.");
            }
            return firstNonOverriddenImpurity;
            // --- MODIFICATION END ---
        }
    }
}