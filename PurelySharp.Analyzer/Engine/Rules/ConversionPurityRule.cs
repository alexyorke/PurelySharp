using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Configuration;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ConversionPurityRule : IPurityRule
    {
        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IConversionOperation conversionOperation)
            {
                PurityAnalysisEngine.LogDebug($"WARNING: ConversionPurityRule called with unexpected operation type: {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"    [ConversionRule] Checking operand of type {conversionOperation.Operand?.Kind} for conversion: {operation.Syntax}");
            if (conversionOperation.Operand == null)
            {
                PurityAnalysisEngine.LogDebug($"    [ConversionRule] Conversion operand is null. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var operandResult = PurityAnalysisEngine.CheckSingleOperation(conversionOperation.Operand, context);

            PurityAnalysisEngine.LogDebug($"    [ConversionRule] Operand Result: IsPure={operandResult.IsPure}");

            return operandResult;
        }

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Conversion);
    }
}