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
                PurityAnalysisEngine.LogDebug($"  [BinaryOpRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [BinaryOpRule] Checking Binary Operation: {binaryOperation.Syntax} (Operator: {binaryOperation.OperatorKind})");

            // 1. Check Left Operand
            var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.LeftOperand, context, currentState);
            if (!leftResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left Operand is Impure: {binaryOperation.LeftOperand.Syntax}");
                return leftResult;
            }

            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left Operand is Pure.");

            // 2. Check Right Operand
            var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.RightOperand, context, currentState);
            if (!rightResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right Operand is Impure: {binaryOperation.RightOperand.Syntax}");
                return rightResult;
            }

            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right Operand is Pure.");

            // 3. Check for user-defined operator method
            if (binaryOperation.OperatorMethod != null)
            {
                // First, check if the operator method is already in the cache
                if (context.PurityCache.TryGetValue(binaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                {
                    if (!cachedResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is IMPURE (cached). Binary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                    }
                    PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is Pure (cached).");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }

                // If not in cache, check if it's a known pure/impure method
                if (PurityAnalysisEngine.IsKnownPureBCLMember(binaryOperation.OperatorMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is known pure BCL member.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }

                if (PurityAnalysisEngine.IsKnownImpure(binaryOperation.OperatorMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is known impure. Binary operation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                }

                // If not known, analyze the operator method recursively
                var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    binaryOperation.OperatorMethod.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods,
                    context.PurityCache);

                if (!operatorPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is IMPURE. Binary operation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                }

                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] User-defined operator method '{binaryOperation.OperatorMethod.Name}' is Pure.");
            }

            // 4. Check if this is a checked operation
            if (binaryOperation.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Operation is checked. Checking operator method purity.");

                // If there's a user-defined operator method for the checked operation
                if (binaryOperation.OperatorMethod != null)
                {
                    // First, check if the operator method is already in the cache
                    if (context.PurityCache.TryGetValue(binaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                    {
                        if (!cachedResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is IMPURE (cached). Binary operation is Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                        }
                        PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is Pure (cached).");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    // If not in cache, check if it's a known pure/impure method
                    if (PurityAnalysisEngine.IsKnownPureBCLMember(binaryOperation.OperatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is known pure BCL member.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    if (PurityAnalysisEngine.IsKnownImpure(binaryOperation.OperatorMethod))
                    {
                        PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is known impure. Binary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                    }

                    // If not known, analyze the operator method recursively
                    var operatorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        binaryOperation.OperatorMethod.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    if (!operatorPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is IMPURE. Binary operation is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(binaryOperation.Syntax);
                    }

                    PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Checked operator method '{binaryOperation.OperatorMethod.Name}' is Pure.");
                }
            }

            // If we get here, all checks passed
            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Binary operation is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}