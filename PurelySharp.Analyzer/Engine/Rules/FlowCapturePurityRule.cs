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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // Flow capture itself is considered a pure operation in terms of side effects.
            // It just captures a value for later use.
            PurityAnalysisEngine.LogDebug($"    [FlowCaptureRule] Treating FlowCapture operation as Pure: {operation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}