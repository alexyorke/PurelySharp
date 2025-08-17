using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ParameterReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ParameterReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IParameterReferenceOperation parameterReference))
            {

                PurityAnalysisEngine.LogDebug($"WARNING: ParameterReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            IParameterSymbol parameterSymbol = parameterReference.Parameter;









            PurityAnalysisEngine.LogDebug($"    [ParamRefRule] Parameter reference '{parameterSymbol.Name}' (RefKind: {parameterSymbol.RefKind}) - Assuming Pure read");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}