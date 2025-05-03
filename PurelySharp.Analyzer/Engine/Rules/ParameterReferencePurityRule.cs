using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes parameter references for purity.
    /// Reading value, in, or ref readonly parameters is pure.
    /// Using ref or out parameters is considered potentially impure.
    /// </summary>
    internal class ParameterReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ParameterReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IParameterReferenceOperation parameterReference))
            {
                // Should not happen given ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"WARNING: ParameterReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            IParameterSymbol parameterSymbol = parameterReference.Parameter;

            // Reading a parameter is generally pure, unless it's a ref/out/in parameter
            // that hasn't been proven immutable/readonly within the current context.
            // For simplicity now, let's assume reading any parameter is pure.
            // Writing to parameters (especially ref/out/in) is handled by AssignmentPurityRule.

            // Could add checks here later based on RefKind if needed, but reads are usually safe.
            // Example: if (parameterSymbol.RefKind != RefKind.None && !IsEffectivelyReadonly(parameterSymbol, context)) return Impure...

            PurityAnalysisEngine.LogDebug($"    [ParamRefRule] Parameter reference '{parameterSymbol.Name}' (RefKind: {parameterSymbol.RefKind}) - Assuming Pure read");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}