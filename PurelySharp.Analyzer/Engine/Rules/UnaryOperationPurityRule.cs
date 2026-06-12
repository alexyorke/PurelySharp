using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class UnaryOperationPurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Unary);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IUnaryOperation unaryOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [UnaryOpRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [UnaryOpRule] Checking Unary Operation: {unaryOperation.Syntax} (Operator: {unaryOperation.OperatorKind})");


            var operandResult = PurityAnalysisEngine.CheckSingleOperation(unaryOperation.Operand, context, currentState);
            if (!operandResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operand is Impure: {unaryOperation.Operand.Syntax}");
                return operandResult;
            }

            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operand is Pure.");

            if (unaryOperation.Operand.Type?.TypeKind == TypeKind.Dynamic ||
                unaryOperation.Type?.TypeKind == TypeKind.Dynamic)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Dynamic unary operation detected. Conservatively treating as Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    unaryOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "dynamic_dispatch",
                        nameof(UnaryOperationPurityRule),
                        unaryOperation,
                        syntaxNode: unaryOperation.Syntax));
            }


            if (unaryOperation.OperatorMethod != null)
            {

                if (IsStaticAbstractInterfaceOperator(unaryOperation.OperatorMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Static abstract interface operator '{unaryOperation.OperatorMethod.Name}' has unresolved dispatch targets. Unary operation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        unaryOperation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unknown_external_call",
                            nameof(UnaryOperationPurityRule),
                            unaryOperation,
                            symbol: unaryOperation.OperatorMethod));
                }

                if (context.PurityCache.TryGetValue(unaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                {
                    if (!cachedResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE (cached). Unary operation is Impure.");
                        return cachedResult.WithCallee(unaryOperation.OperatorMethod, unaryOperation.Syntax);
                    }
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is Pure (cached).");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }


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


                var operatorPurity = PurityAnalysisEngine.GetCalleePurity(unaryOperation.OperatorMethod, context);

                if (!operatorPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE. Unary operation is Impure.");
                    return operatorPurity.WithCallee(unaryOperation.OperatorMethod, unaryOperation.Syntax);
                }

                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] User-defined operator method '{unaryOperation.OperatorMethod.Name}' is Pure.");
            }


            if (unaryOperation.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Operation is checked. Checking operator method purity.");


                if (unaryOperation.OperatorMethod != null)
                {

                    if (context.PurityCache.TryGetValue(unaryOperation.OperatorMethod.OriginalDefinition, out var cachedResult))
                    {
                        if (!cachedResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE (cached). Unary operation is Impure.");
                            return cachedResult.WithCallee(unaryOperation.OperatorMethod, unaryOperation.Syntax);
                        }
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is Pure (cached).");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }


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


                    var operatorPurity = PurityAnalysisEngine.GetCalleePurity(unaryOperation.OperatorMethod, context);

                    if (!operatorPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is IMPURE. Unary operation is Impure.");
                        return operatorPurity.WithCallee(unaryOperation.OperatorMethod, unaryOperation.Syntax);
                    }

                    PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Checked operator method '{unaryOperation.OperatorMethod.Name}' is Pure.");
                }
            }


            PurityAnalysisEngine.LogDebug($"    [UnaryOpRule] Unary operation is Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool IsStaticAbstractInterfaceOperator(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsStatic &&
                methodSymbol.IsAbstract &&
                methodSymbol.MethodKind == MethodKind.UserDefinedOperator &&
                methodSymbol.ContainingType?.TypeKind == TypeKind.Interface;
        }
    }
}
