using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class StructuralPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Block,
            OperationKind.MethodBodyOperation,
            OperationKind.Try,
            OperationKind.CatchClause,
            OperationKind.VariableDeclarationGroup,
            OperationKind.VariableDeclaration,
            OperationKind.VariableDeclarator,
            OperationKind.Labeled,
            OperationKind.Empty,
            OperationKind.FlowCapture,
            OperationKind.FieldInitializer,
            OperationKind.PropertyInitializer
            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {

            PurityAnalysisEngine.LogDebug($"    [StructuralRule] Structural operation ({operation.Kind}) - Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}