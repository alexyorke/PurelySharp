using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of array element reference operations (e.g., array[i]).
    /// Reading from an array element is generally considered pure.
    /// </summary>
    internal class ArrayElementReferencePurityRule : IPurityRule
    {
        // Use OperationKind.ArrayElementReference
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.ArrayElementReference };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IArrayElementReferenceOperation arrayElementReference))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // We are only interested in *reading* the array element here.
            // Writing to array elements (e.g., array[i] = value) is handled by AssignmentPurityRule.
            if (IsPartOfAssignmentTarget(arrayElementReference))
            {
                PurityAnalysisEngine.LogDebug($"ArrayElementReferencePurityRule: Skipping array element read as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Reading an array element by index is typically pure.
            // The purity depends on the array reference and the index expression(s),
            // which should be evaluated by other rules.

            PurityAnalysisEngine.LogDebug($"ArrayElementReferencePurityRule: Assuming pure for array element read.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool IsPartOfAssignmentTarget(IOperation operation)
        {
            IOperation? current = operation;
            while (current != null)
            {
                if (current.Parent is IAssignmentOperation assignment && assignment.Target == current)
                {
                    return true;
                }
                // Stop if we go beyond the immediate parent assignment check
                // Updated to include ArrayElementReference
                if (!(current.Parent is IMemberReferenceOperation || current.Parent is IPropertyReferenceOperation || current.Parent is IArrayElementReferenceOperation))
                {
                    break;
                }
                current = current.Parent;
            }
            return false;
        }
    }
}