using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of array initializer operations.
    /// </summary>
    internal class ArrayInitializerPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayInitializer);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IArrayInitializerOperation arrayInitializer))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: ArrayInitializerPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"ArrayInitializerRule: Analyzing {arrayInitializer.Syntax}");

            // Assume the array initializer operation itself is pure.
            // The purity of the *elements* within the initializer
            // should be determined by the rules analyzing the operations that compute those elements.

            PurityAnalysisEngine.LogDebug($"ArrayInitializerRule: Assuming initializer operation itself is pure for {arrayInitializer.Syntax}. Element purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed IsElementConsideredPure helper as element analysis is deferred to other rules.
    }
}