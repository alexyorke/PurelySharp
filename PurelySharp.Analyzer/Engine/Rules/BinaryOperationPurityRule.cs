using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

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


            var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.LeftOperand, context, currentState);
            if (!leftResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left Operand is Impure: {binaryOperation.LeftOperand.Syntax}");
                return leftResult;
            }

            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Left Operand is Pure.");


            var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryOperation.RightOperand, context, currentState);
            if (!rightResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right Operand is Impure: {binaryOperation.RightOperand.Syntax}");
                return rightResult;
            }

            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Right Operand is Pure.");


            if (binaryOperation.OperatorMethod != null)
            {

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


            if (binaryOperation.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Operation is checked. Checking operator method purity.");


                if (binaryOperation.OperatorMethod != null)
                {

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


            PurityAnalysisEngine.LogDebug($"    [BinaryOpRule] Binary operation is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}