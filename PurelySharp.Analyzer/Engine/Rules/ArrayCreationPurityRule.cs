using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of array creation operations.
    /// </summary>
    internal class ArrayCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayCreation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IArrayCreationOperation arrayCreation))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Analyzing {arrayCreation.Syntax}");

            // Array creation allocates memory and returns a mutable array, consider it impure.
            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Array creation '{arrayCreation.Syntax}' is impure (mutable allocation).");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(arrayCreation.Syntax);

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

            // Check dimension sizes
            foreach (var dimensionSize in arrayCreation.DimensionSizes)
            {
                PurityAnalysisEngine.LogDebug($"    [ArrayCreationRule] Checking dimension size: {dimensionSize.Syntax} ({dimensionSize.Kind})");
                var sizeResult = PurityAnalysisEngine.CheckSingleOperation(dimensionSize, context, currentState);
                if (!sizeResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ArrayCreationRule] Dimension size expression is Impure. Array creation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(arrayCreation.Syntax);
                }
            }

            // Check initializer (if present)
            if (arrayCreation.Initializer != null)
            {
                PurityAnalysisEngine.LogDebug($"    [ArrayCreationRule] Checking initializer: {arrayCreation.Initializer.Syntax} ({arrayCreation.Initializer.Kind})");
                var initializerResult = PurityAnalysisEngine.CheckSingleOperation(arrayCreation.Initializer, context, currentState);
                if (!initializerResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ArrayCreationRule] Initializer is Impure. Array creation is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(arrayCreation.Syntax);
                }
            }

            PurityAnalysisEngine.LogDebug($"ArrayCreationRule: Array creation '{arrayCreation.Syntax}' determined to be Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}