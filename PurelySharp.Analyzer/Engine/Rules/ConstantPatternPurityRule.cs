using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes constant pattern operations for purity.
    /// Constant patterns are always pure.
    /// </summary>
    internal class ConstantPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ConstantPattern);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // Constant patterns are inherently pure.
            PurityAnalysisEngine.LogDebug($"    [ConstantPatternRule] Constant pattern ({operation.Syntax}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}