using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes 'with' expressions for potential side effects.
    /// </summary>
    internal class WithOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.With);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IWithOperation withOperation)
            {
                // Should not happen if Applicability is correct
                PurityAnalysisEngine.LogDebug($"[WithRule] Warning: Incorrect operation type {operation.Kind}.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // A 'with' expression creates a new object instance.
            // For value types (structs), creating a new value is considered pure as it doesn't modify existing state directly.
            // For reference types (classes), creating a new object involves allocation, which we generally consider impure.
            ITypeSymbol? targetType = withOperation.Type;

            if (targetType == null)
            {
                PurityAnalysisEngine.LogDebug($"[WithRule] Could not determine type for 'with' expression. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(withOperation.Syntax);
            }

            // First, check the operand
            if (withOperation.Operand != null)
            {
                PurityAnalysisEngine.LogDebug($"[WithRule] Checking Operand: {withOperation.Operand.Kind}");
                var operandResult = PurityAnalysisEngine.CheckSingleOperation(withOperation.Operand, context);
                if (!operandResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"[WithRule] Operand is impure.");
                    return operandResult;
                }
            }

            // Then, check the initializer (if it exists)
            if (withOperation.Initializer != null)
            {
                PurityAnalysisEngine.LogDebug($"[WithRule] Checking Initializer: {withOperation.Initializer.Kind}");
                var initializerResult = PurityAnalysisEngine.CheckSingleOperation(withOperation.Initializer, context);
                if (!initializerResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"[WithRule] Initializer is impure.");
                    return initializerResult;
                }
            }

            // If operand and initializer are pure, the final purity depends on the type.
            if (targetType.IsValueType)
            {
                PurityAnalysisEngine.LogDebug($"[WithRule] 'with' expression on value type '{targetType.ToDisplayString()}' with pure children. Result: Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }
            else // Reference Type
            {
                PurityAnalysisEngine.LogDebug($"[WithRule] 'with' expression on reference type '{targetType.ToDisplayString()}' with pure children. Result: Impure (Object Creation)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(withOperation.Syntax);
            }
        }
    }
}