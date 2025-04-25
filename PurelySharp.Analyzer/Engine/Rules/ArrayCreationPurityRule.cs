using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of array creation operations.
    /// </summary>
    internal class ArrayCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayCreation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IArrayCreationOperation arrayCreation)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Analyzing {arrayCreation.Syntax}");

            // Simplification: Assume the array creation *operation itself* is pure.
            // The purity of the ELEMENTS within the initializer (if any) should be checked
            // by the rule that *consumes* this array creation (e.g., ObjectCreationPurityRule
            // when handling ParamArray, or AssignmentPurityRule when assigning an array).
            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Assuming array creation operation itself is pure for {arrayCreation.Syntax}. Element purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;

            /* // --- Old logic that checked initializers --- (Now handled by consuming rules)
            if (arrayCreation.Initializer != null)
            {
                PurityAnalysisEngine.LogDebug($"  Checking {arrayCreation.Initializer.ElementValues.Length} initializer elements...");
                foreach (var elementValue in arrayCreation.Initializer.ElementValues)
                {
                    var elementPurity = PurityAnalysisEngine.CheckSingleOperation(elementValue, context);
                    if (!elementPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"  Initializer element '{elementValue.Syntax}' is Impure. Result: Impure.");
                        return elementPurity;
                    }
                    PurityAnalysisEngine.LogDebug($"  Initializer element '{elementValue.Syntax}' is Pure.");
                }
                PurityAnalysisEngine.LogDebug($"  All initializer elements are Pure.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"  No initializer elements to check.");
            }

            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Array creation '{arrayCreation.Syntax}' determined to be Pure.");
            return PurityAnalysisResult.Pure;
            */
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}