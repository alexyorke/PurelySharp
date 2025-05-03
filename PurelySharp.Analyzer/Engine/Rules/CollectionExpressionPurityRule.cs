using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq; // Required for LINQ operations like Any
using System.Collections.Generic; // For IEnumerable
using System.Collections.Immutable; // For ImmutableArray.Create
using PurelySharp.Analyzer.Engine; // Add this

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
        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
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
                PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Could not determine target type. Assuming IMPURE (conservative default). Element purity handled elsewhere.");
                // *** Assume IMPURE if type is unknown, as default is often mutable array ***
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(collectionExpression.Syntax);
            }
            // --- End Check --- 

            // If we reached here, it means the target type was resolved and was immutable.
            PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target type is known immutable or analysis completed. Final Result: PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}