using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of binary operations (+, -, *, /, %, &, |, ^, <<, >>, >>>, ==, !=, <, <=, >, >=, etc.).
    /// Binary operations are generally pure assuming their operands are pure.
    /// </summary>
    internal class BinaryOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Binary };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IBinaryOperation binaryOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Most standard binary operators are pure by themselves.
            // The purity depends on the operands, which should be evaluated by other rules
            // before reaching this point in the CFG analysis.
            // For example, if an operand is an impure method call, the MethodInvocationPurityRule
            // should have marked the state as impure already.
            // Therefore, we can generally consider the binary operation itself pure.

            // Check Left Operand
            PurityAnalysisEngine.LogDebug($"    [BinaryOp] Checking Left Operand: {binaryOperation.LeftOperand?.Kind}");
            if (binaryOperation.LeftOperand != null)
            {
                var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.LeftOperand, context);
                if (!leftResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOp] Left Operand is IMPURE. Result: Impure.");
                    return leftResult;
                }
                PurityAnalysisEngine.LogDebug($"    [BinaryOp] Left Operand is Pure.");
            }

            // Check Right Operand
            PurityAnalysisEngine.LogDebug($"    [BinaryOp] Checking Right Operand: {binaryOperation.RightOperand?.Kind}");
            if (binaryOperation.RightOperand != null)
            {
                var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.RightOperand, context);
                if (!rightResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOp] Right Operand is IMPURE. Result: Impure.");
                    return rightResult;
                }
                PurityAnalysisEngine.LogDebug($"    [BinaryOp] Right Operand is Pure.");
            }

            // If both operands are pure, check the operator method itself if it's user-defined
            IMethodSymbol? operatorMethodSymbol = binaryOperation.OperatorMethod;
            if (operatorMethodSymbol != null && !operatorMethodSymbol.IsImplicitlyDeclared) // Check if it's explicitly user-defined
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOp] Checking user-defined operator method: {operatorMethodSymbol.ToDisplayString()}");
                // Analyze the operator method itself using the internal recursive checker
                var operatorResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    operatorMethodSymbol.OriginalDefinition, // Use original definition for consistency
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods,
                    context.PurityCache); // Use the cache passed in the context

                if (!operatorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOp] User-defined operator method {operatorMethodSymbol.ToDisplayString()} is IMPURE. Result: Impure.");
                    // If the operator is impure, use its impurity result (potentially more specific node)
                    return operatorResult;
                }
                PurityAnalysisEngine.LogDebug($"    [BinaryOp] User-defined operator method {operatorMethodSymbol.ToDisplayString()} is Pure.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOp] Using built-in or implicitly declared operator ({binaryOperation.OperatorKind}). Assuming pure operation.");
            }

            // If operands and user-defined operator (if any) are pure, the operation is pure.
            PurityAnalysisEngine.LogDebug($"    [BinaryOp] Operands and operator method (if applicable) pure. Binary operation {binaryOperation.OperatorKind} is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}