using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes await operations for purity.
    /// </summary>
    internal class AwaitPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Await);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is IAwaitOperation awaitOperation)
            {
                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Analyzing awaited operation {awaitOperation.Operation.Kind}");

                // The purity of 'await' depends entirely on the purity of the expression being awaited.
                // Recursively check the purity of the awaited operation.
                var awaitedOperationResult = PurityAnalysisEngine.CheckSingleOperation(awaitOperation.Operation, context);

                if (!awaitedOperationResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Awaited operation {awaitOperation.Operation.Kind} is impure.");
                    // Report impurity based on the result from the awaited operation check.
                    return awaitedOperationResult;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Awaited operation {awaitOperation.Operation.Kind} is pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
            }

            // Should not happen if ApplicableOperationKinds is correct
            PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure (Defensive).");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}