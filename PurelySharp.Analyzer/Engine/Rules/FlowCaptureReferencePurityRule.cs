using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class FlowCaptureReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.FlowCaptureReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // A FlowCaptureReference simply refers to a value captured earlier.
            // The purity depends on the operation that *created* the captured value, 
            // which should have been analyzed previously in the CFG.
            // Therefore, the reference itself is considered pure.
            PurityAnalysisEngine.LogDebug($"    [FlowCaptureRefRule] Treating FlowCaptureReference as Pure: {operation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}