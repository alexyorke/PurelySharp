using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class LocalFunctionOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.LocalFunction);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            _ = (ILocalFunctionOperation)operation;
            // Local function bodies are validated after the main CFG pass (see PurityAnalysisEngine
            // "Post-CFG: Checking Unreachable Local Functions") and again when invoked via GetCalleePurity.
            // Walking DescendantsAndSelf here hit operation kinds with no registered rule and defaulted to impure.
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}
