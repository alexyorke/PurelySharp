using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq; // Added for DescendantsAndSelf()

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule that checks loop constructs (for, foreach, while, do).
    /// Currently assumes loops are impure if they contain operations, 
    /// as body analysis is complex.
    /// </summary>
    internal class LoopPurityRule : IPurityRule
    {
        // Covers For, ForEach, While, DoWhile loops
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Loop);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ILoopOperation loopOperation))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: LoopPurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure for safety.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            PurityAnalysisEngine.LogDebug($"    [LoopRule] Analyzing loop body for: {loopOperation.Syntax}");

            // Analyze operations within the loop body directly.
            // We need to check all descendants, not just immediate children.
            if (loopOperation.Body != null)
            {
                foreach (var bodyOp in loopOperation.Body.DescendantsAndSelf())
                {
                    // Use the helper function to check each operation within the body
                    var opResult = PurityAnalysisEngine.CheckSingleOperation(bodyOp, context);
                    if (!opResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to operation in loop body: {bodyOp.Kind} at {bodyOp.Syntax.GetLocation()?.GetLineSpan().StartLinePosition}");
                        return opResult; // Loop is impure if any body operation is impure
                    }
                }
            }

            // If all body operations are pure (or body is null/empty),
            // consider the loop structure itself pure for now.
            // (Ignoring potential issues with loop conditions or increments for simplicity)
            PurityAnalysisEngine.LogDebug($"    [LoopRule] Loop body analyzed as pure for: {loopOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}