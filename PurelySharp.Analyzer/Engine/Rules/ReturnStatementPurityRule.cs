using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule that checks return statements.
    /// Generally pure, but might check the returned value's purity.
    /// </summary>
    internal class ReturnStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.Return };

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IReturnOperation returnOperation))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // A simple return (return;) is always pure.
            if (returnOperation.ReturnedValue == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Get the operation representing the returned value.
            IOperation returnedValueOperation = returnOperation.ReturnedValue;

            // Find the rule applicable to the returned value's operation kind
            // and delegate the purity check to that rule.
            // Access the rules list from the context
            foreach (var rule in context.PurityRules)
            {
                if (rule.ApplicableOperationKinds.Contains(returnedValueOperation.Kind))
                {
                    // Delegate the check to the specific rule for the returned value
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Delegating check for returned value ({returnedValueOperation.Kind}) to {rule.GetType().Name}");
                    var valuePurityResult = rule.CheckPurity(returnedValueOperation, context);
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Delegated check result: IsPure={valuePurityResult.IsPure}");
                    // If the returned value is impure, the return statement is impure.
                    if (!valuePurityResult.IsPure)
                    {
                        return valuePurityResult; // Propagate the impure result (with specific node)
                    }
                    else
                    {
                        // Returned value is pure, so the return statement itself is pure.
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                }
            }

            // If no specific rule was found for the returned value's kind,
            // default to impure to be safe.
            PurityAnalysisEngine.LogDebug($"    [ReturnRule] No rule found for returned value kind {returnedValueOperation.Kind}. Defaulting to impure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(returnedValueOperation.Syntax);
        }
    }
}