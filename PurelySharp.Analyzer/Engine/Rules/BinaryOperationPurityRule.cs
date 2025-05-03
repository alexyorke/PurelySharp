using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of binary operations (+, -, *, /, %, &, |, ^, <<, >>, >>>, ==, !=, <, <=, >, >=, etc.).
    /// Binary operations are generally pure assuming their operands are pure.
    /// </summary>
    internal class BinaryOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Binary);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IBinaryOperation binaryOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [BinaryOpRule] Checking Binary Operation: {binaryOperation.Syntax} (Operator: {binaryOperation.OperatorKind})");

            // 1. Check Left Operand
            var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.LeftOperand, context, currentState);
            if (!leftResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left operand is Impure: {binaryOperation.LeftOperand.Syntax}");
                return leftResult;
            }
            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left operand is Pure.");

            // 2. Check Right Operand
            var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.RightOperand, context, currentState);
            if (!rightResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right operand is Impure: {binaryOperation.RightOperand.Syntax}");
                return rightResult;
            }
            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right operand is Pure.");

            // Check for user-defined operator method
            if (binaryOperation.OperatorMethod != null && !IsPureOperator(binaryOperation.OperatorMethod, context))
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is IMPURE. Binary operation is Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
            }

            // If both operands and user-defined operator (if any) are pure, the operation is pure.
            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Operands and operator method (if applicable) pure. Binary operation {binaryOperation.OperatorKind} is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private bool IsPureOperator(IMethodSymbol operatorMethod, PurityAnalysisContext context)
        {
            // Use the main engine's recursive check
            var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                operatorMethod.OriginalDefinition,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache);

            return operatorPurity.IsPure;
        }
    }
}