using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of unary operations (+, -, !, ~).
    /// </summary>
    internal class UnaryOperationPurityRule : IPurityRule
    {
        // Handle unary plus, minus, logical not, bitwise complement
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Unary);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IUnaryOperation unaryOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [UnaryRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [UnaryOpRule] Checking Unary Operation: {unaryOperation.Syntax} (Operator: {unaryOperation.OperatorKind})");

            // 1. Check the Operand
            var operandResult = PurityAnalysisEngine.CheckSingleOperation(unaryOperation.Operand, context, currentState);
            if (!operandResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operand is Impure: {unaryOperation.Operand.Syntax}");
                return operandResult;
            }

            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operand is Pure. Unary operation is Pure.");

            // 2. Check for user-defined operator method
            if (unaryOperation.OperatorMethod != null && !IsPureOperator(unaryOperation.OperatorMethod, context))
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE. Unary operation is Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private bool IsPureOperator(IMethodSymbol operatorMethod, PurityAnalysisContext context)
        {
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