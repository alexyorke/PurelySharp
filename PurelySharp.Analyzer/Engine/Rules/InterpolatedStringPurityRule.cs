using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable; // Required for ImmutableArray

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes the purity of string interpolation operations.
    /// </summary>
    internal class InterpolatedStringPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.InterpolatedString);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IInterpolatedStringOperation interpolatedString))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: InterpolatedStringPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"InterpolatedStringPurityRule: Analyzing {interpolatedString.Syntax}");

            // Assume the interpolation operation itself is pure.
            // The purity of the *parts* (interpolated expressions)
            // should be determined by the rules analyzing the operations that compute those parts.
            // Example: $"Hello {ImpureMethod()}" - MethodInvocationPurityRule handles ImpureMethod().

            PurityAnalysisEngine.LogDebug($"InterpolatedStringPurityRule: Assuming interpolation operation itself is pure for {interpolatedString.Syntax}. Part purity handled elsewhere.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}