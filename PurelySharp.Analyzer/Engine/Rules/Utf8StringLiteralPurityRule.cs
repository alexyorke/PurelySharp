using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class Utf8StringLiteralPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray<OperationKind>.Empty;

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (operation is IUtf8StringOperation utf8StringOperation)
            {

                PurityAnalysisEngine.LogDebug($"Utf8StringLiteralPurityRule: Utf8String operation ({utf8StringOperation.Syntax}) - Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            PurityAnalysisEngine.LogDebug($"Utf8StringLiteralPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure (Defensive).");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}