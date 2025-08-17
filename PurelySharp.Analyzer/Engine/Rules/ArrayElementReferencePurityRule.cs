using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ArrayElementReferencePurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayElementReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IArrayElementReferenceOperation arrayElementReference))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }



            if (IsPartOfAssignmentTarget(arrayElementReference))
            {
                PurityAnalysisEngine.LogDebug($"ArrayElementReferencePurityRule: Skipping array element read as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }






            PurityAnalysisEngine.LogDebug($"    [ArrayElemRefRule] Checking array reference: {arrayElementReference.ArrayReference.Syntax} ({arrayElementReference.ArrayReference.Kind})");
            var arrayRefResult = PurityAnalysisEngine.CheckSingleOperation(arrayElementReference.ArrayReference, context, currentState);
            if (!arrayRefResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [ArrayElemRefRule] Array reference is Impure. Element reference is Impure.");
                return PurityAnalysisEngine.ImpureResult(arrayElementReference.ArrayReference.Syntax);
            }


            foreach (var indexOperation in arrayElementReference.Indices)
            {
                PurityAnalysisEngine.LogDebug($"    [ArrayElemRefRule] Checking index: {indexOperation.Syntax} ({indexOperation.Kind})");
                var indexResult = PurityAnalysisEngine.CheckSingleOperation(indexOperation, context, currentState);
                if (!indexResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ArrayElemRefRule] Index expression is Impure. Element reference is Impure.");
                    return PurityAnalysisEngine.ImpureResult(indexOperation.Syntax);
                }
            }

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