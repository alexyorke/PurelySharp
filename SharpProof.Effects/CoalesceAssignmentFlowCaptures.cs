using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharpProof.Effects;

internal sealed class CoalesceAssignmentFlowCaptures : OperationFlowCaptures
{
    protected override bool IsRelevant(IFlowCaptureOperation capture)
    {
        return capture.Syntax.AncestorsAndSelf()
            .Any(static syntax => syntax is AssignmentExpressionSyntax
            {
                RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind
                    .CoalesceAssignmentExpression
            });
    }
}
