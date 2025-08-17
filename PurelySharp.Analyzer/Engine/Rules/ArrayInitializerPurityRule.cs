using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ArrayInitializerPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayInitializer);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IArrayInitializerOperation arrayInitializer))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: ArrayInitializerPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"ArrayInitializerRule: Analyzing {arrayInitializer.Syntax}");






            foreach (var elementValue in arrayInitializer.ElementValues)
            {
                var elementResult = PurityAnalysisEngine.CheckSingleOperation(elementValue, context, currentState);
                if (!elementResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ArrayInitRule] Element initializer is Impure: {elementValue.Syntax}");
                    return PurityAnalysisEngine.ImpureResult(elementValue.Syntax);
                }
            }

            PurityAnalysisEngine.LogDebug($"ArrayInitializerRule: Assuming initializer operation itself is pure for {arrayInitializer.Syntax}. Element purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }


    }
}