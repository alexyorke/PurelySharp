using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule that handles FlowCapture operations.
    /// These are generally introduced by the compiler for temporary storage 
    /// and don't represent side effects themselves.
    /// </summary>
    internal class FlowCapturePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.FlowCapture);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // Flow captures themselves don't have side effects; they represent intermediate values.
            // The purity depends on the captured value's computation.
            PurityAnalysisEngine.LogDebug($"    [FlowCaptureRule] Treating FlowCapture operation as Pure: {operation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}