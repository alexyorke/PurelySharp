using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles typeof() operations.
    /// typeof() is inherently pure.
    /// </summary>
    internal class TypeOfPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.TypeOf);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // typeof() operations are always pure.
            PurityAnalysisEngine.LogDebug($"    [TypeOfRule] TypeOf operation ({operation.Syntax}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}