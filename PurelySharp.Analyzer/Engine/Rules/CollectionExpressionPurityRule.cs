using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class CollectionExpressionPurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.CollectionExpression);


        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ICollectionExpressionOperation collectionExpression))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: CollectionExpressionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"CollectionExpressionRule: Analyzing {collectionExpression.Syntax}");


            ITypeSymbol? targetType = collectionExpression.Type;
            if (targetType != null)
            {
                string targetTypeName = targetType.OriginalDefinition.ToDisplayString();
                PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target Type is {targetTypeName}");



                if (!targetTypeName.StartsWith("System.Collections.Immutable", StringComparison.Ordinal))
                {



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

                return PurityAnalysisEngine.PurityAnalysisResult.Impure(collectionExpression.Syntax);
            }



            PurityAnalysisEngine.LogDebug($" CollectionExpressionRule: Target type is known immutable or analysis completed. Final Result: PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }


    }
}