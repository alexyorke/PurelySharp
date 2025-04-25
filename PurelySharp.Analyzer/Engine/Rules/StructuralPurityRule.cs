using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Handles purely structural operations like blocks or method bodies.
    /// These are considered pure themselves; the purity depends on their contents,
    /// which are visited by the walker or analyzed by CFG.
    /// </summary>
    internal class StructuralPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Block,
            OperationKind.MethodBodyOperation
            // Add other structural kinds if needed (e.g., FieldInitializer?)
            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // These operations are just containers. Assume pure structure.
            PurityAnalysisEngine.LogDebug($"    [StructuralRule] Structural operation ({operation.Kind}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}