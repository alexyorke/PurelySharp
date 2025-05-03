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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
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

            // Check each part of the interpolated string
            foreach (var part in interpolatedString.Parts)
            {
                PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking part: {part.Syntax} ({part.Kind})");

                PurityAnalysisEngine.PurityAnalysisResult partResult;

                if (part is IInterpolatedStringTextOperation)
                {
                    // Text parts are always pure
                    partResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else if (part is IInterpolationOperation interpolation)
                {
                    // For interpolation parts, analyze the expression inside { }
                    PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking Interpolation Expression: {interpolation.Expression.Syntax}");
                    partResult = PurityAnalysisEngine.CheckSingleOperation(interpolation.Expression, context, currentState);

                    // Also check the format string and alignment if present (rarely impure, but possible)
                    if (partResult.IsPure && interpolation.Alignment != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking Interpolation Alignment: {interpolation.Alignment.Syntax}");
                        partResult = PurityAnalysisEngine.CheckSingleOperation(interpolation.Alignment, context, currentState);
                    }
                    if (partResult.IsPure && interpolation.FormatString != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking Interpolation FormatString: {interpolation.FormatString.Syntax}");
                        partResult = PurityAnalysisEngine.CheckSingleOperation(interpolation.FormatString, context, currentState);
                    }
                }
                else
                {
                    // Fallback for unexpected part kinds (shouldn't happen often)
                    PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Unexpected part kind: {part.Kind}. Checking generically.");
                    partResult = PurityAnalysisEngine.CheckSingleOperation(part, context, currentState);
                }

                if (!partResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Part is IMPURE. Interpolated string is Impure.");
                    return PurityAnalysisEngine.ImpureResult(part.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}