using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PureMethodAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnforcePureMethodAnalyzerCodeFixProvider)), Shared]
    public class EnforcePureMethodAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(PureMethodAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Enables the code fix to be applied to all occurrences in the solution.
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            // Find the diagnostic to fix.
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the method declaration identified by the diagnostic.
            var token = root.FindToken(diagnosticSpan.Start);
            var methodDeclaration = token.Parent?.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>().FirstOrDefault();

            if (methodDeclaration == null) return;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make method body empty",
                    createChangedDocument: c => MakeMethodBodyEmptyAsync(context.Document, methodDeclaration, c),
                    equivalenceKey: "MakeMethodBodyEmpty"),
                diagnostic);
        }

        private async Task<Document> MakeMethodBodyEmptyAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            // Remove all statements from the method body.
            MethodDeclarationSyntax newMethodDecl;

            if (methodDecl.Body != null)
            {
                // Replace the method body with an empty block.
                var emptyBody = SyntaxFactory.Block();
                newMethodDecl = methodDecl.WithBody(emptyBody).WithExpressionBody(null).WithSemicolonToken(default);
            }
            else if (methodDecl.ExpressionBody != null)
            {
                // Remove the expression body and replace with an empty block.
                var emptyBody = SyntaxFactory.Block();
                newMethodDecl = methodDecl.WithBody(emptyBody).WithExpressionBody(null).WithSemicolonToken(default);
            }
            else
            {
                // The method has no body (e.g., it's abstract), so no changes are needed.
                return document;
            }

            // Replace the old method with the new method in the syntax tree.
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document;

            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            if (newRoot == null) return document;

            // Return the updated document.
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
