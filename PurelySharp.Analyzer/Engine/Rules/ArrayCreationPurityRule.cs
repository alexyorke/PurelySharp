using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine; // Import static members like PurityAnalysisResult

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of array creation operations.
    /// </summary>
    internal class ArrayCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayCreation);

        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            if (!(operation is IArrayCreationOperation arrayCreation))
            {
                return PurityAnalysisResult.Pure; // Should not happen
            }

            LogDebug($"ArrayCreationRule: Analyzing {arrayCreation.Syntax}");

            // Check if this array creation is directly used for a 'params' parameter argument.
            bool isParamsArray = arrayCreation.Parent is IArgumentOperation argumentOperation &&
                                argumentOperation.Parameter != null &&
                                argumentOperation.Parameter.IsParams;

            if (isParamsArray)
            {
                // LogDebug($"    [ArrCreateRule] Array creation '{arrayCreation.Syntax}' is for a 'params' parameter '{argumentOperation.Parameter.Name}'. Checking initializer elements."); // Removed log causing build error
                // If it's for params, the allocation itself is generally acceptable.
                // Check the *initializer elements* for purity.
                if (arrayCreation.Initializer != null)
                {
                    foreach (var elementValue in arrayCreation.Initializer.ElementValues)
                    {
                        var elementPurity = CheckSingleOperation(elementValue, context, currentState);
                        if (!elementPurity.IsPure)
                        {
                            LogDebug($"    [ArrCreateRule] 'params' array initializer element '{elementValue.Syntax}' is IMPURE. Operation is Impure.");
                            return PurityAnalysisResult.Impure(elementPurity.ImpureSyntaxNode ?? elementValue.Syntax);
                        }
                    }
                    LogDebug($"    [ArrCreateRule] All 'params' array initializer elements are Pure.");
                }
                else
                {
                    LogDebug($"    [ArrCreateRule] 'params' array has no initializer elements to check.");
                }
                // If initializer is null or all elements are pure, treat this params allocation as pure.
                LogDebug($"    [ArrCreateRule] 'params' array creation itself treated as PURE.");
                return PurityAnalysisResult.Pure;
            }
            else
            {
                // Regular array creation allocates memory and returns a mutable array, consider it impure.
                LogDebug($"    [ArrCreateRule] Array creation '{arrayCreation.Syntax}' is IMPURE (mutable allocation, not for params).");
                return PurityAnalysisResult.Impure(arrayCreation.Syntax);
            }
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}