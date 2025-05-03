using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes declaration patterns (e.g., 'var x', 'int x') for purity.
    /// A declaration pattern itself is pure; impurity comes from initialization or usage.
    /// </summary>
    internal class DeclarationPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.DeclarationPattern);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // Declaration patterns themselves don't cause side effects.
            // Potential impurity comes from initializers (handled elsewhere if present)
            // or how the declared variable is used later.
            PurityAnalysisEngine.LogDebug($"  [DeclarationPatternRule] Declaration Pattern ({operation.Syntax}) is considered Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}