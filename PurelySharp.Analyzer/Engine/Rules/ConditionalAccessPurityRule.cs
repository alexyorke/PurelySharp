using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ConditionalAccessPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.ConditionalAccess };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IConditionalAccessOperation conditionalAccessOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [ConditionalAccessRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [ConditionalAccessRule] Checking Conditional Access Operation: {conditionalAccessOperation.Syntax}");


            var operationResult = PurityAnalysisEngine.CheckSingleOperation(conditionalAccessOperation.Operation, context, currentState);
            if (!operationResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [ConditionalAccessRule] Operation before '?.' is Impure: {conditionalAccessOperation.Operation.Syntax}");
                return operationResult;
            }
            PurityAnalysisEngine.LogDebug($"    [ConditionalAccessRule] Operation before '?.' is Pure.");


            var whenNotNullResult = PurityAnalysisEngine.CheckSingleOperation(conditionalAccessOperation.WhenNotNull, context, currentState);
            if (!whenNotNullResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [ConditionalAccessRule] Operation after '?.' (WhenNotNull) is Impure: {conditionalAccessOperation.WhenNotNull.Syntax}");
                return whenNotNullResult;
            }
            PurityAnalysisEngine.LogDebug($"    [ConditionalAccessRule] Operation after '?.' (WhenNotNull) is Pure.");


            PurityAnalysisEngine.LogDebug($"  [ConditionalAccessRule] Conditional Access Operation is Pure: {conditionalAccessOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}