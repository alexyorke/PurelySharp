using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes binary patterns (e.g., 'or', 'and' patterns) for purity.
    /// </summary>
    internal class BinaryPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.BinaryPattern);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IBinaryPatternOperation binaryPatternOperation)
            {
                // Should not happen if ApplicableOperationKinds is correct
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"  [BinaryPatternRule] Checking Binary Pattern: {binaryPatternOperation.Syntax}");

            // Check Left Pattern
            if (binaryPatternOperation.LeftPattern != null)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Checking Left Pattern: {binaryPatternOperation.LeftPattern.Kind}");
                var leftResult = PurityAnalysisEngine.CheckSingleOperation(binaryPatternOperation.LeftPattern, context);
                if (!leftResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Left Pattern is IMPURE.");
                    return leftResult;
                }
            }

            // Check Right Pattern
            if (binaryPatternOperation.RightPattern != null)
            {
                PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Checking Right Pattern: {binaryPatternOperation.RightPattern.Kind}");
                var rightResult = PurityAnalysisEngine.CheckSingleOperation(binaryPatternOperation.RightPattern, context);
                if (!rightResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [BinaryPatternRule] Right Pattern is IMPURE.");
                    return rightResult;
                }
            }

            // If both sides are pure (or null), the binary pattern itself is pure.
            PurityAnalysisEngine.LogDebug($"  [BinaryPatternRule] Binary Pattern is PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}