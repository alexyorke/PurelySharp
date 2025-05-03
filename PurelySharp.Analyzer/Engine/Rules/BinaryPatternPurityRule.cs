using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes binary patterns (e.g., 'or', 'and' patterns) for purity.
    /// </summary>
    internal class BinaryPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.BinaryPattern);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IBinaryPatternOperation binaryPatternOperation))
            {
                // Should not happen if ApplicableOperationKinds is correct
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [BinaryPatternRule] Checking Binary Pattern: {binaryPatternOperation.Syntax}");

            // Check the left pattern
            var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryPatternOperation.LeftPattern, context, currentState);
            if (!leftResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Left pattern is Impure: {binaryPatternOperation.LeftPattern.Syntax}");
                return leftResult;
            }

            PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Left pattern is Pure.");

            // Check the right pattern
            var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryPatternOperation.RightPattern, context, currentState);
            if (!rightResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Right pattern is Impure: {binaryPatternOperation.RightPattern.Syntax}");
                return rightResult;
            }

            // If both sides are pure (or null), the binary pattern itself is pure.
            PurityAnalysisEngine.LogDebug($"  [BinaryPatternRule] Binary Pattern is PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}