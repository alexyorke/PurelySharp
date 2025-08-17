using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class CheckedExpressionPurityRule : IPurityRule
    {
















        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Binary,
            OperationKind.Unary

            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {

            bool isChecked = false;
            IMethodSymbol? operatorMethod = null;

            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Found Binary Operation with IsChecked=true: {operation.Syntax}");
                isChecked = true;
                operatorMethod = binaryOp.OperatorMethod;


                var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryOp.LeftOperand, context, currentState);
                if (!leftResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Left operand is Impure: {binaryOp.LeftOperand.Syntax}");
                    return leftResult;
                }

                var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryOp.RightOperand, context, currentState);
                if (!rightResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Right operand is Impure: {binaryOp.RightOperand.Syntax}");
                    return rightResult;
                }
            }
            else if (operation is IUnaryOperation unaryOp && unaryOp.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Found Unary Operation with IsChecked=true: {operation.Syntax}");
                isChecked = true;
                operatorMethod = unaryOp.OperatorMethod;


                var operandResult = PurityAnalysisEngine.CheckSingleOperation(unaryOp.Operand, context, currentState);
                if (!operandResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Operand is Impure: {unaryOp.Operand.Syntax}");
                    return operandResult;
                }
            }

            if (isChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Processing checked operation: {operation.Syntax}");


                if (operatorMethod != null)
                {

                    if (context.PurityCache.TryGetValue(operatorMethod.OriginalDefinition, out var cachedResult))
                    {
                        if (!cachedResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is IMPURE (cached). Operation is Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                        }
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is Pure (cached).");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }


                    if (PurityAnalysisEngine.IsKnownPureBCLMember(operatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is known pure BCL member.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    if (PurityAnalysisEngine.IsKnownImpure(operatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is known impure. Operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                    }


                    var operatorPurity = PurityAnalysisEngine.GetCalleePurity(operatorMethod, context);

                    if (!operatorPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is IMPURE. Operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                    }

                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is Pure.");
                }


                if (context.ContainingMethodSymbol != null &&
                    PurityAnalysisEngine.IsPureEnforced(context.ContainingMethodSymbol, context.EnforcePureAttributeSymbol))
                {
                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Operation is part of a method marked with [EnforcePure]. Checking purity of containing method.");


                    var containingMethodPurity = PurityAnalysisEngine.GetCalleePurity(context.ContainingMethodSymbol, context);

                    if (!containingMethodPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Containing method is IMPURE. Operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                    }
                }



                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operation is Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}