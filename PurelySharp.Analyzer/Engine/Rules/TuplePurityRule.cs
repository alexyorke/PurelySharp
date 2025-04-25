using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles tuple operations (e.g., (a, b) = (1, 2)).
    /// A tuple itself is pure; its purity depends on the contained elements/expressions.
    /// </summary>
    internal class TuplePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Tuple);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ITupleOperation tupleOperation))
            {
                PurityAnalysisEngine.LogDebug($"    [TupleRule] WARNING: Not an ITupleOperation ({operation.Kind}). Assuming pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"    [TupleRule] Checking Tuple operation ({operation.Syntax})...");

            // Check each element within the tuple
            foreach (var element in tupleOperation.Elements)
            {
                var elementResult = PurityAnalysisEngine.CheckSingleOperation(element, context);
                if (!elementResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [TupleRule] Tuple IMPURE due to element: {element.Syntax}");
                    return elementResult;
                }
            }

            PurityAnalysisEngine.LogDebug($"    [TupleRule] Tuple operation ({operation.Syntax}) - All elements Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}