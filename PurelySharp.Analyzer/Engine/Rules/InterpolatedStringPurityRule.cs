using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

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






            PurityAnalysisEngine.LogDebug($"InterpolatedStringPurityRule: Assuming interpolation operation itself is pure for {interpolatedString.Syntax}. Part purity handled elsewhere.");


            foreach (var part in interpolatedString.Parts)
            {
                PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking part: {part.Syntax} ({part.Kind})");

                PurityAnalysisEngine.PurityAnalysisResult partResult;

                if (part is IInterpolatedStringTextOperation)
                {

                    partResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else if (part is IInterpolationOperation interpolation)
                {

                    PurityAnalysisEngine.LogDebug($"    [InterpStrRule] Checking Interpolation Expression: {interpolation.Expression.Syntax}");
                    partResult = PurityAnalysisEngine.CheckSingleOperation(interpolation.Expression, context, currentState);


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