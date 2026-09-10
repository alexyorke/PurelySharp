using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharpProof.Effects;

internal sealed class ConditionalTruthOperatorFlowCaptures : OperationFlowCaptures
{
    protected override bool IsRelevant(IFlowCaptureOperation capture)
    {
        return capture.Syntax.AncestorsAndSelf().Any(static syntax => syntax is
            BinaryExpressionSyntax
        {
            RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression or
                    (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression
        });
    }
}
