using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Configuration;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ConversionPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Conversion);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (operation is not IConversionOperation conversionOperation)
            {
                PurityAnalysisEngine.LogDebug($"WARNING: ConversionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"    [ConversionRule] Checking operand of type {conversionOperation.Operand?.Kind} for conversion: {operation.Syntax}");
            if (conversionOperation.Operand == null)
            {
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Conversion operand is null. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var operandResult = PurityAnalysisEngine.CheckSingleOperation(conversionOperation.Operand, context, currentState);

            PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operand Result: IsPure={operandResult.IsPure}");

            // If operand is impure, the conversion is impure.
            if (!operandResult.IsPure)
            {
                return operandResult;
            }

            // If operand is pure, THEN check the conversion operator itself if it's user-defined
            if (conversionOperation.Conversion.IsUserDefined && conversionOperation.Conversion.MethodSymbol != null)
            {
                IMethodSymbol operatorMethod = conversionOperation.Conversion.MethodSymbol;
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] User-defined conversion found: {operatorMethod.ToDisplayString()}");
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Conversion Syntax: {conversionOperation.Syntax}");
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Conversion Op Kind: {conversionOperation.Kind}");

                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Checking operator method: {operatorMethod.ToDisplayString()}");
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operator Method Containing Type: {operatorMethod.ContainingType?.ToDisplayString()}");
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operator Method Return Type: {operatorMethod.ReturnType?.ToDisplayString()}");
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operator Method Param Count: {operatorMethod.Parameters.Length}");

                // Use the recursive internal method, passing context's visited set and cache
                var operatorResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    operatorMethod.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods, // Pass the visited set from context
                    context.PurityCache // Pass the cache from context
                );

                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operator Method Result: IsPure={operatorResult.IsPure}");

                // If the operator method analysis yields a result (pure or impure), return it.
                // Adjust the impure node if necessary to point to the original conversion syntax.
                if (!operatorResult.IsPure)
                {
                    // Return impure result, using the conversion operation's syntax as the location if operatorResult didn't specify one
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(operatorResult.ImpureSyntaxNode ?? conversionOperation.Syntax);
                }
                else
                {
                    // Operator is pure, and we already established operand is pure.
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
            }

            // If operand was pure and it wasn't a user-defined conversion (or symbol was null), return the (pure) operand result.
            return operandResult;
        }
    }
}