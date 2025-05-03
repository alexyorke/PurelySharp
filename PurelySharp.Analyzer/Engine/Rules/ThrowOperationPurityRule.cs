using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ThrowOperationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Throw);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IThrowOperation throwOperation))
            {
                // Should not happen
                PurityAnalysisEngine.LogDebug($"  [ThrowRule] WARNING: Incorrect operation type {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            // Analyze the purity of the expression being thrown.
            // The act of throwing disrupts control flow, but doesn't cause impurity itself
            // unless evaluating the exception object has side effects.
            if (throwOperation.Exception != null)
            {
                PurityAnalysisEngine.LogDebug($"    [ThrowRule] Checking exception expression: {throwOperation.Exception.Syntax} ({throwOperation.Exception.Kind})");
                var exceptionResult = PurityAnalysisEngine.CheckSingleOperation(throwOperation.Exception, context, currentState);
                if (!exceptionResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ThrowRule] Exception expression is IMPURE. Throw is Impure.");
                    return exceptionResult; // Impurity comes from creating/evaluating the exception
                }
                PurityAnalysisEngine.LogDebug($"    [ThrowRule] Exception expression is PURE.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"  [ThrowRule] Found re-throw operation (exception is null). Assuming pure evaluation.");
            }

            // If evaluating the exception is pure, the throw operation itself doesn't introduce impurity.
            // Control flow disruption is handled by CFG.
            PurityAnalysisEngine.LogDebug($"  [ThrowRule] Throw operation evaluation is pure. Returning Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}