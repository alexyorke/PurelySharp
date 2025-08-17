using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class AwaitPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Await);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IAwaitOperation awaitOperation))
            {

                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure (Defensive).");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Analyzing awaited operation {awaitOperation.Operation.Kind}");
            PurityAnalysisEngine.LogDebug($"  [AwaitRule] Checking Await Operation: {awaitOperation.Syntax}");


            var awaitedExpressionResult = PurityAnalysisEngine.CheckSingleOperation(awaitOperation.Operation, context, currentState);

            if (!awaitedExpressionResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"AwaitPurityRule: Awaited operation {awaitOperation.Operation.Kind} is impure.");

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