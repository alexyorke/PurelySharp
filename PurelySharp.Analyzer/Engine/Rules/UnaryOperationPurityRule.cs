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
                PurityAnalysisEngine.LogDebug($"  [UnaryOpRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
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

            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operand is Pure.");

            // 2. Check for user-defined operator method
            if (unaryOperation.OperatorMethod != null)
            {
                // First, check if the operator method is already in the cache
                if (context.PurityCache.TryGetValue(unaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                {
                    if (!cachedResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE (cached). Unary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                    }
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is Pure (cached).");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }

                // If not in cache, check if it's a known pure/impure method
                if (PurityAnalysisEngine.IsKnownPureBCLMember(unaryOperation.OperatorMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is known pure BCL member.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }

                if (PurityAnalysisEngine.IsKnownImpure(unaryOperation.OperatorMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is known impure. Unary operation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                }

                // If not known, analyze the operator method recursively
                var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    unaryOperation.OperatorMethod.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods,
                    context.PurityCache);

                if (!operatorPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE. Unary operation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                }

                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is Pure.");
            }

            // 3. Check if this is a checked operation
            if (unaryOperation.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operation is checked. Checking operator method purity.");

                // If there's a user-defined operator method for the checked operation
                if (unaryOperation.OperatorMethod != null)
                {
                    // First, check if the operator method is already in the cache
                    if (context.PurityCache.TryGetValue(unaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                    {
                        if (!cachedResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE (cached). Unary operation is Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                        }
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is Pure (cached).");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    // If not in cache, check if it's a known pure/impure method
                    if (PurityAnalysisEngine.IsKnownPureBCLMember(unaryOperation.OperatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is known pure BCL member.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    if (PurityAnalysisEngine.IsKnownImpure(unaryOperation.OperatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is known impure. Unary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                    }

                    // If not known, analyze the operator method recursively
                    var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        unaryOperation.OperatorMethod.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!operatorPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE. Unary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(unaryOperation.Syntax);
                    }

                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is Pure.");
                }
            }

            // If we get here, all checks passed
            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Unary operation is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}