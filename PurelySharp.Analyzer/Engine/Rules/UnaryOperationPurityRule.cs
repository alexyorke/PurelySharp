using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of unary operations (+, -, !, ~).
    /// </summary>
    internal class UnaryOperationPurityRule : IPurityRule
    {
        // Handle unary plus, minus, logical not, bitwise complement
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[]
        {
             OperationKind.UnaryOperator, 
             // OperationKind.UnaryPlus, // Obsolete? Use UnaryOperator
             // OperationKind.UnaryMinus, // Obsolete? Use UnaryOperator
             // OperationKind.BitwiseNegation, // Obsolete? Use UnaryOperator
             // OperationKind.LogicalNot // Obsolete? Use UnaryOperator
        };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IUnaryOperation unaryOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [UnaryRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [UnaryRule] Checking Unary Operation: {unaryOperation.OperatorKind} on {unaryOperation.Syntax}");

            // Check the operand's purity
            var operandResult = PurityAnalysisEngine.CheckSingleOperation(unaryOperation.Operand, context);
            if (!operandResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryRule] Operand is Impure: {unaryOperation.Operand.Syntax}");
                return operandResult;
            }

            PurityAnalysisEngine.LogDebug($"    [UnaryRule] Operand is Pure. Unary operation is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}