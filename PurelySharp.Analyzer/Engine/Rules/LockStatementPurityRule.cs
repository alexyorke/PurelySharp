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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (operation is not ILockOperation lockOperation)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            // Lock statements inherently involve synchronization, which is impure unless explicitly allowed.
            PurityAnalysisEngine.LogDebug($"    [LockRule] Lock statement ({operation.Syntax}) - Impure by default");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(lockOperation.Syntax);
        }
    }
}