using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq; // Required for LINQ operations like Any
using System.Collections.Generic; // For IEnumerable
using System.Collections.Immutable; // For ImmutableArray.Create

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of collection expression operations (C# 12 feature).
    /// </summary>
    internal class CollectionExpressionPurityRule : IPurityRule
    {
        // This rule specifically handles ICollectionExpressionOperation
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.CollectionExpression);

        // Return type matches interface: PurityAnalysisEngine.PurityAnalysisResult (non-nullable)
        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is ICollectionExpressionOperation collectionExpression))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: CollectionExpressionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"CollectionExpressionRule: Analyzing {collectionExpression.Syntax}");

            // --- Check Target Type --- 
            ITypeSymbol? targetType = collectionExpression.Type;
            if (targetType != null)
            {
                string targetTypeName = targetType.OriginalDefinition.ToDisplayString();
                PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target Type is {targetTypeName}");

                // Check if the target type is a known mutable collection or array
                // Explicitly allow System.Collections.Immutable types
                if (!targetTypeName.StartsWith("System.Collections.Immutable", StringComparison.Ordinal))
                {
                    // Could add more specific checks here (e.g., List<T>, Dictionary<T>, T[] etc.)
                    // For now, assume any non-immutable target makes the creation potentially impure
                    // as it might allow subsequent mutation.
                    PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target type '{targetTypeName}' is not known immutable. Marking IMPURE.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(collectionExpression.Syntax);
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target type '{targetTypeName}' is immutable. Marking PURE.");
                }
            }
            else
            {
                PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Could not determine target type. Assuming PURE (optimistic). Element purity handled elsewhere.");
            }
            // --- End Check --- 

            // If the target type is immutable (or couldn't be determined), assume the expression itself is pure.
            // The purity of the *elements* within the collection expression
            // should be determined by the rules analyzing the operations that compute those elements.
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}