using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal sealed class NegatedPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.NegatedPattern);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(
            IOperation operation,
            PurityAnalysisContext context,
            PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            foreach (var child in operation.ChildOperations)
            {
                var childResult = PurityAnalysisEngine.CheckSingleOperation(child, context, currentState);
                if (!childResult.IsPure)
                {
                    return childResult;
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}
