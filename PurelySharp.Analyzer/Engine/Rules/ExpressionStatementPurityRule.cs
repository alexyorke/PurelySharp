using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule that handles ExpressionStatement operations.
    /// An ExpressionStatement is pure if the expression it contains is pure.
    /// </summary>
    internal class ExpressionStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ExpressionStatement);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IExpressionStatementOperation expressionStatement))
            {
                // Should not happen given ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"WARNING: ExpressionStatementPurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            // Check the purity of the underlying expression using the engine's helper
            PurityAnalysisEngine.LogDebug($"    [ExprStmtRule] Checking underlying expression of kind {expressionStatement.Operation?.Kind} for statement: {operation.Syntax}");
            if (expressionStatement.Operation == null)
            {
                PurityAnalysisEngine.LogDebug($"    [ExprStmtRule] ExpressionStatement has null underlying operation. Assuming Pure (e.g., empty statement?).");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Or Impure? Let's assume pure for now.
            }

            // Use the recursive check for the inner operation
            var innerResult = PurityAnalysisEngine.CheckSingleOperation(expressionStatement.Operation, context);
            PurityAnalysisEngine.LogDebug($"    [ExprStmtRule] Inner operation ({expressionStatement.Operation.Kind}) result: IsPure={innerResult.IsPure}");

            return innerResult;
        }
    }
}