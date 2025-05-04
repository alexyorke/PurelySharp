using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles default value operations, which are considered pure.
    /// </summary>
    internal class DefaultValuePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.DefaultValue);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            PurityAnalysisEngine.LogDebug($"    [DefaultValueRule] DefaultValue operation - Always Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}