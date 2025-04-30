using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes UTF-8 string literal operations for purity.
    /// </summary>
    internal class Utf8StringLiteralPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Utf8String);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is IUtf8StringOperation utf8StringOperation)
            {
                // UTF-8 string literals themselves are constant values and have no side effects.
                PurityAnalysisEngine.LogDebug($"Utf8StringLiteralPurityRule: Utf8String operation ({utf8StringOperation.Syntax}) - Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Should not happen
            PurityAnalysisEngine.LogDebug($"Utf8StringLiteralPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure (Defensive).");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}