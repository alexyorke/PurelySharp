using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Considers lock statements as impure.
    /// </summary>
    internal class LockStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Lock);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not ILockOperation lockOperation)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            // Lock statements introduce potential blocking and thread interaction, making them impure.
            // We could potentially check context.AllowSynchronizationAttributeSymbol here if we want
            // to allow locks when the attribute is present, but for now, treat all locks as impure.
            PurityAnalysisEngine.LogDebug($"  [LockRule] Detected lock statement. IMPURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(lockOperation.Syntax);
        }
    }
}