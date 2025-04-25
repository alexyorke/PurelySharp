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

            // If both operands are pure, the binary operation itself is pure (assuming no impure overloaded operator)
            // TODO: Add check for impure overloaded operators if ISymbol can be obtained from IBinaryOperation

            // Potential future enhancements:
            // - Check for overloaded operators that might be impure.
            PurityAnalysisEngine.LogDebug($"    [BinaryOp] Both operands pure. Binary operation {binaryOperation.OperatorKind} is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}