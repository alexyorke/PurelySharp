using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of return statements.
    /// The statement itself is pure, but the returned value might not be.
    /// </summary>
    internal class ReturnStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Return };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IReturnOperation returnOperation))
            {
                // Should not happen
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // If there's no returned value (e.g., return; in a void method), it's pure.
            if (returnOperation.ReturnedValue == null)
            {
                PurityAnalysisEngine.LogDebug("    [ReturnRule] No returned value - Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Check the purity of the returned value expression itself.
            IOperation returnedValueOperation = returnOperation.ReturnedValue;

            PurityAnalysisEngine.LogDebug($"    [ReturnRule] Checking returned value ({returnedValueOperation.Kind})");
            var valuePurityResult = PurityAnalysisEngine.CheckSingleOperation(returnedValueOperation, context);
            PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value check result: IsPure={valuePurityResult.IsPure}, Node Type={valuePurityResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");

            // Propagate the result from the returned value check.
            if (!valuePurityResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value is impure. Propagating result.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value is pure. Return statement is Pure.");
            }
            return valuePurityResult;
        }
    }
}