using Microsoft.CodeAnalysis;
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
                    return bodyResult;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Could not get symbol for anonymous function. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(anonymousFunction.Syntax);
                }
            }
            else if (target is IMethodReferenceOperation methodReference)
            {

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Found MethodReferenceOperation: {methodReference.Method.ToDisplayString()}. Analyzing target method.");
                IMethodSymbol targetMethodSymbol = methodReference.Method;


                var methodResult = PurityAnalysisEngine.GetCalleePurity(targetMethodSymbol, context);

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Referenced method analysis result: IsPure={methodResult.IsPure}");
                return methodResult;
            }
            else
            {

                PurityAnalysisEngine.LogDebug($"    [DelegateCreationRule] Unexpected DelegateCreation target kind: {target.Kind}. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(target.Syntax);
            }
        }
    }
}