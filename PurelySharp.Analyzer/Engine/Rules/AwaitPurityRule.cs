using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes await operations for purity.
    /// </summary>
    internal class AwaitPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Await);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IAwaitOperation awaitOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure (Defensive).");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Analyzing awaited operation {awaitOperation.Operation.Kind}");
            PurityAnalysisEngine.LogDebug($"  [AwaitRule] Checking Await Operation: {awaitOperation.Syntax}");

            // Check the expression being awaited
            var awaitedExpressionResult = PurityAnalysisEngine.CheckSingleOperation(awaitOperation.Operation, context, currentState);

            if (!awaitedExpressionResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Awaited operation {awaitOperation.Operation.Kind} is impure.");
                // Report impurity based on the result from the awaited operation check.
                return awaitedExpressionResult;
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Awaited operation {awaitOperation.Operation.Kind} is pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }
        }
    }
}