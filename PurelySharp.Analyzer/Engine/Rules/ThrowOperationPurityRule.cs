using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ThrowOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Throw };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IThrowOperation throwOperation))
            {
                // Should not happen
                PurityAnalysisEngine.LogDebug($"  [ThrowRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            PurityAnalysisEngine.LogDebug($"  [ThrowRule] Found throw operation: {throwOperation.Syntax}. Always impure.");

            // A throw operation always disrupts normal flow and is considered impure.
            // We don't need to analyze the thrown exception expression's purity,
            // as the act of throwing itself is the impurity.
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(throwOperation.Syntax);
        }
    }
}