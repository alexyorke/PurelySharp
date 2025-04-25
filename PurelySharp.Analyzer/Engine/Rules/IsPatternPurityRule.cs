using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
// using PurelySharp.Analyzer.Engine; // Keep this commented/removed for now to avoid ambiguity
using System.Collections.Generic;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine; // Use static using

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class IsPatternPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => new[] { OperationKind.IsPattern };

        // Use PurityAnalysisResult directly (should resolve via static using)
        // Parameter PurityAnalysisContext is defined in this namespace
        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // An IsPattern operation checks if an input value matches a given pattern.
            // Example: x is int i, obj is Point { X: 0, Y: > 0 }
            // The operation itself (the matching) is generally pure.
            // Impurity would typically come from the Input expression or
            // potentially complex patterns involving method calls (though those
            // should be handled recursively by analyzing the pattern's operations).

            var isPatternOperation = (IIsPatternOperation)operation;

            // Call CheckSingleOperation directly (static using)
            PurityAnalysisResult inputPurity = CheckSingleOperation(isPatternOperation.Value, context);
            if (!inputPurity.IsPure)
            {
                // Call LogDebug directly (static using)
                LogDebug($"    [IsPatternRule] Input expression '{isPatternOperation.Value.Syntax?.ToString() ?? "N/A"}' is impure.");
                return inputPurity; // Propagate impurity from the input
            }

            // 2. Analyze the Pattern itself (patterns can contain operations)
            // The Pattern operation itself (e.g., IConstantPatternOperation, IDeclarationPatternOperation)
            // might have sub-expressions or calls that need checking.
            // We rely on the CFG visiting these sub-operations or the CheckSingleOperation
            // recursion handling them if the pattern itself is an IOperation.
            // For simple patterns like constant or type patterns, the pattern check is pure.
            // More complex patterns might require specific handling if they introduce impurity directly.
            // For now, assume the pattern structure itself doesn't add impurity beyond its components.

            // Example: If pattern is `ConstantPattern { Value: MethodCall() }`,
            // the MethodCall() should be analyzed separately by the engine.

            // Call LogDebug directly (static using)
            LogDebug($"    [IsPatternRule] Assuming pattern itself is pure, input was pure. Syntax: '{operation.Syntax?.ToString() ?? "N/A"}'");
            // Return PurityAnalysisResult.Pure (static using)
            return PurityAnalysisResult.Pure;
        }
    }
}