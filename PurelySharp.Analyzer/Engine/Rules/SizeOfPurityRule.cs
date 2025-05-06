using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of sizeof operations.
    /// </summary>
    internal class SizeOfPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.SizeOf);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ISizeOfOperation sizeOfOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [SizeOfRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [SizeOfRule] Checking SizeOf Operation: {sizeOfOperation.Syntax}");

            // sizeof operations are always pure
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}