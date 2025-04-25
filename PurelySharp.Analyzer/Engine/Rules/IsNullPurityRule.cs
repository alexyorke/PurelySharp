using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine; // Use static using

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of IsNull operations (e.g., 'x is null').
    /// </summary>
    internal class IsNullPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.IsNull };

        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // Cast to IUnaryOperation as IIsNullOperation might not be available
            if (!(operation is IUnaryOperation unaryOperation))
            {
                LogDebug($"    [IsNullRule] Operation is not IUnaryOperation. Kind: {operation.Kind}");
                // Should not happen if ApplicableOperationKinds is correct, but handle defensively
                return PurityAnalysisResult.Impure(operation.Syntax);
            }

            // The null check itself is pure.
            // Impurity comes only from evaluating the operand being checked.
            PurityAnalysisResult operandPurity = CheckSingleOperation(unaryOperation.Operand, context);

            if (!operandPurity.IsPure)
            {
                LogDebug($"    [IsNullRule] Operand '{unaryOperation.Operand.Syntax?.ToString() ?? "N/A"}' is impure.");
                return operandPurity;
            }

            LogDebug($"    [IsNullRule] Operand was pure. Operation is pure. Syntax: '{operation.Syntax?.ToString() ?? "N/A"}'");
            return PurityAnalysisResult.Pure;
        }
    }
}