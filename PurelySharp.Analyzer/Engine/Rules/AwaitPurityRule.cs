using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
                return CheckAwaitPatternMembers(awaitOperation, context);
            }
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckAwaitPatternMembers(
            IAwaitOperation awaitOperation,
            PurityAnalysisContext context)
        {
            if (awaitOperation.Syntax is not AwaitExpressionSyntax awaitSyntax)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var awaitInfo = context.SemanticModel.GetAwaitExpressionInfo(awaitSyntax);

            var getAwaiterResult = CheckAwaitPatternMethod(awaitInfo.GetAwaiterMethod, awaitOperation.Syntax, context);
            if (!getAwaiterResult.IsPure)
            {
                return getAwaiterResult;
            }

            var isCompletedResult = CheckAwaitPatternMethod(awaitInfo.IsCompletedProperty?.GetMethod, awaitOperation.Syntax, context);
            if (!isCompletedResult.IsPure)
            {
                return isCompletedResult;
            }

            return CheckAwaitPatternMethod(awaitInfo.GetResultMethod, awaitOperation.Syntax, context);
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckAwaitPatternMethod(
            IMethodSymbol? methodSymbol,
            SyntaxNode awaitSyntax,
            PurityAnalysisContext context)
        {
            if (methodSymbol == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var originalDefinition = methodSymbol.OriginalDefinition;
            if (!ShouldAnalyzeAwaitPatternMember(originalDefinition))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var result = PurityAnalysisEngine.GetCalleePurity(originalDefinition, context);
            return result.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : result.WithCallee(originalDefinition, awaitSyntax);
        }

        private static bool ShouldAnalyzeAwaitPatternMember(IMethodSymbol methodSymbol)
        {
            return methodSymbol.DeclaringSyntaxReferences.Length > 0 ||
                   PurityAnalysisEngine.IsKnownImpure(methodSymbol) ||
                   PurityAnalysisEngine.HasImpureAttribute(methodSymbol);
        }
    }
}
