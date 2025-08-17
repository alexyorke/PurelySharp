using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class LoopPurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Loop);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ILoopOperation loopOperation))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: LoopPurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure for safety.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            PurityAnalysisEngine.LogDebug($"    [LoopRule] Analyzing loop body for: {loopOperation.Syntax}");



            if (loopOperation.Body != null)
            {
                foreach (var bodyOp in loopOperation.Body.DescendantsAndSelf())
                {

                    var opResult = PurityAnalysisEngine.CheckSingleOperation(bodyOp, context, currentState);
                    if (!opResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to operation in loop body: {bodyOp.Kind} at {bodyOp.Syntax.GetLocation()?.GetLineSpan().StartLinePosition}");
                        return opResult;
                    }
                }
            }




            PurityAnalysisEngine.LogDebug($"    [LoopRule] Loop body analyzed as pure for: {loopOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}