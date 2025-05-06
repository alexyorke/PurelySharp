using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule to flag 'checked' context operations. The actual purity check is deferred.
    /// </summary>
    internal class CheckedExpressionPurityRule : IPurityRule
    {
        // Note: There isn't a specific OperationKind for CheckedExpression itself in older Roslyn versions.
        // We might need to rely on syntax analysis or find how CheckedExpression is represented in IOperation tree.
        // Let's assume OperationKind.UnaryOperator might cover some checked cases or it's handled implicitly.
        // Adding OperationKind.Conversion as sometimes checked involves implicit conversions.
        // A more robust approach might involve syntax node analysis if IOperation doesn't expose it directly.
        // For now, let's target unary and conversion as potential hosts.
        // Update: Roslyn seems to represent checked/unchecked expressions via IUnaryOperation (for +/-)
        // and potentially IConversionOperation or others depending on context. Let's stick with Unary for now.
        // It seems `checked()` around binary ops might not directly yield a distinct IOperation kind easily accessible?
        // Let's refine ApplicableOperationKinds later if needed.
        // Let's assume for now CheckedExpression might be part of Unary or Binary directly.
        // Re-evaluating: Checked/Unchecked are distinct OperationKinds in later Roslyn, but not netstandard2.0's Microsoft.CodeAnalysis 3.3.1.
        // Checked *expressions* (like `checked(a+b)`) are represented by IBinaryOperation/IUnaryOperation with IsChecked=true.
        // Checked *statements* (`checked { ... }`) are OperationKind.CheckedStatement.
        // Let's make this rule target Binary and Unary and check the IsChecked flag.

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Binary,
            OperationKind.Unary
            // Add Conversion if needed
            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // Check if this is a checked operation
            bool isChecked = false;
            IMethodSymbol? operatorMethod = null;

            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Found Binary Operation with IsChecked=true: {operation.Syntax}");
                isChecked = true;
                operatorMethod = binaryOp.OperatorMethod;

                // Check the operands first
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

                // Check the operand first
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

                // If there's a user-defined operator method, check its purity
                if (operatorMethod != null)
                {
                    // First, check if the operator method is already in the cache
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

                    // If not in cache, check if it's a known pure/impure method
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

                    // If not known, analyze the operator method recursively
                    var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        operatorMethod.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!operatorPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is IMPURE. Operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                    }

                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operator method '{operatorMethod.Name}' is Pure.");
                }

                // Check if this operation is part of a method marked with [EnforcePure]
                if (context.ContainingMethodSymbol != null &&
                    PurityAnalysisEngine.IsPureEnforced(context.ContainingMethodSymbol, context.EnforcePureAttributeSymbol))
                {
                    PurityAnalysisEngine.LogDebug($"    [CheckedRule] Operation is part of a method marked with [EnforcePure]. Checking purity of containing method.");

                    // Check the containing method's purity
                    var containingMethodPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        context.ContainingMethodSymbol.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!containingMethodPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [CheckedRule] Containing method is IMPURE. Operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
                    }
                }

                // If we get here, either there was no user-defined operator or it was pure
                // The actual operation purity will be determined by the Binary/Unary rules
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Checked operation is Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Not a checked operation, let other rules handle it
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}