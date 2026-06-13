using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class DelegateCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.DelegateCreation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IDelegateCreationOperation delegateCreation))
            {

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Unexpected operation kind {operation.Kind}. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }


            IOperation target = delegateCreation.Target;

            if (target is IAnonymousFunctionOperation anonymousFunction)
            {
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found AnonymousFunctionOperation. Analyzing its body.");


                IMethodSymbol lambdaSymbol = anonymousFunction.Symbol;
                if (lambdaSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Recursively checking lambda: {lambdaSymbol.ToDisplayString()}");

                    var bodyResult = PurityAnalysisEngine.GetCalleePurity(lambdaSymbol, context);

                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Lambda body analysis result: IsPure={bodyResult.IsPure}");
                    return bodyResult.IsPure
                        ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                        : bodyResult.WithCallee(lambdaSymbol, delegateCreation.Syntax);
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Could not get symbol for anonymous function. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(anonymousFunction.Syntax);
                }
            }
            else if (target is IFlowAnonymousFunctionOperation flowAnonymousFunction)
            {
                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found FlowAnonymousFunctionOperation. Analyzing its body.");

                IMethodSymbol lambdaSymbol = flowAnonymousFunction.Symbol;
                if (lambdaSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Recursively checking flow lambda: {lambdaSymbol.ToDisplayString()}");

                    var bodyResult = PurityAnalysisEngine.GetCalleePurity(lambdaSymbol, context);

                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Flow lambda body analysis result: IsPure={bodyResult.IsPure}");
                    return bodyResult.IsPure
                        ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                        : bodyResult.WithCallee(lambdaSymbol, delegateCreation.Syntax);
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Could not get symbol for flow anonymous function. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(flowAnonymousFunction.Syntax);
                }
            }
            else if (target is IMethodReferenceOperation methodReference)
            {

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found MethodReferenceOperation: {methodReference.Method.ToDisplayString()}. Analyzing target method.");
                IMethodSymbol targetMethodSymbol = methodReference.Method;

                if (methodReference.Instance != null)
                {
                    var instanceResult = PurityAnalysisEngine.CheckSingleOperation(methodReference.Instance, context, currentState);
                    if (!instanceResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Method group receiver is impure: {methodReference.Instance.Syntax}");
                        return instanceResult;
                    }
                }

                var potentialTargets = PurityAnalysisEngine.ResolvePotentialTargets(delegateCreation, currentState, context.SemanticModel);
                if (potentialTargets == null || potentialTargets.Value.IsUnresolved)
                {
                    PurityAnalysisEngine.LogDebug("    [DelegateCreationRule] Delegate target could dispatch to unresolved runtime target. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        delegateCreation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unresolved_delegate_target",
                            nameof(DelegateCreationPurityRule),
                            delegateCreation,
                            symbol: targetMethodSymbol));
                }

                foreach (var targetMethod in potentialTargets.Value.MethodSymbols)
                {
                    var methodResult = PurityAnalysisEngine.GetCalleePurity(targetMethod, context);

                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Referenced method analysis result for {targetMethod.ToDisplayString()}: IsPure={methodResult.IsPure}");
                    if (!methodResult.IsPure)
                    {
                        return methodResult.WithCallee(targetMethod, delegateCreation.Syntax);
                    }
                }

                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }
            else
            {

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Unexpected DelegateCreation target kind: {target.Kind}. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(target.Syntax);
            }
        }
    }
}
