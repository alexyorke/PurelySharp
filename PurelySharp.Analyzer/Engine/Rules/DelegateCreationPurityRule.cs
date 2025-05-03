using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of delegate creation operations (lambdas, anonymous methods).
    /// </summary>
    internal class DelegateCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.DelegateCreation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IDelegateCreationOperation delegateCreation))
            {
                // Should not happen based on ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Unexpected operation kind {operation.Kind}. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            // The crucial part is the target method (lambda or anonymous method)
            IOperation target = delegateCreation.Target;

            if (target is IAnonymousFunctionOperation anonymousFunction)
            {
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found AnonymousFunctionOperation. Analyzing its body.");
                // Analyze the body of the anonymous function/lambda
                // The symbol represents the lambda itself
                IMethodSymbol lambdaSymbol = anonymousFunction.Symbol;
                if (lambdaSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Recursively checking lambda: {lambdaSymbol.ToDisplayString()}");
                    // Use DeterminePurityRecursiveInternal to handle caching and cycle detection
                    var bodyResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        lambdaSymbol.OriginalDefinition,
                        context.SemanticModel, // Use context's semantic model
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);

                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Lambda body analysis result: IsPure={bodyResult.IsPure}");
                    return bodyResult; // Delegate creation purity matches lambda body purity
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Could not get symbol for anonymous function. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(anonymousFunction.Syntax);
                }
            }
            else if (target is IMethodReferenceOperation methodReference)
            {
                // Creation of a delegate from an existing method group
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found MethodReferenceOperation: {methodReference.Method.ToDisplayString()}. Analyzing target method.");
                IMethodSymbol targetMethodSymbol = methodReference.Method;

                // Recursively check the purity of the referenced method
                var methodResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    targetMethodSymbol.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods,
                    context.PurityCache);

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Referenced method analysis result: IsPure={methodResult.IsPure}");
                return methodResult; // Delegate creation purity matches target method purity
            }
            else
            {
                // Unexpected target type
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Unexpected DelegateCreation target kind: {target.Kind}. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(target.Syntax);
            }
        }
    }
}