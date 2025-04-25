using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes switch case operations for purity.
    /// A switch case itself (case X:, default:) is pure control flow.
    /// The clause/body of the case is handled by subsequent operations in the CFG.
    /// </summary>
    internal class SwitchCasePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.CaseClause); // Note: OperationKind is CaseClause

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // A case clause label is pure control flow.
            PurityAnalysisEngine.LogDebug($"    [SwitchCaseRule] CaseClause operation ({operation.Syntax}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}