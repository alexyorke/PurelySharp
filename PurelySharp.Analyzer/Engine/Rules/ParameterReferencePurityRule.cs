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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IParameterReferenceOperation parameterReference))
            {
                // Should not happen given ApplicableOperationKinds
                PurityAnalysisEngine.LogDebug($"WARNING: ParameterReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            IParameterSymbol parameterSymbol = parameterReference.Parameter;

            switch (parameterSymbol.RefKind)
            {
                case RefKind.None: // Value parameter
                case RefKind.In:   // In parameter
                case (RefKind)4: // RefKind.RefReadOnlyParameter (value = 4, check needed for older targets)
                    PurityAnalysisEngine.LogDebug($"    [ParamRefRule] ParameterReference '{parameterSymbol.Name}' (RefKind={parameterSymbol.RefKind}) - Pure");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;

                case RefKind.Ref:
                case RefKind.Out:
                    PurityAnalysisEngine.LogDebug($"    [ParamRefRule] ParameterReference '{parameterSymbol.Name}' (RefKind={parameterSymbol.RefKind}) - Impure");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);

                default:
                    // Should not happen, but treat unknown RefKind as potentially impure
                    PurityAnalysisEngine.LogDebug($"    [ParamRefRule] ParameterReference '{parameterSymbol.Name}' (RefKind={parameterSymbol.RefKind}) - Unknown RefKind, Assuming Impure");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }
        }
    }
}