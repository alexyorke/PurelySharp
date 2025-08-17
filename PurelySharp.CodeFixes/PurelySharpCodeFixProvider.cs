using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace PurelySharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PurelySharpCodeFixProvider)), Shared]
    public class PurelySharpCodeFixProvider : CodeFixProvider
    {
        private const string DiagnosticId = "PMA0001";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var methodDecl = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (methodDecl == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove [EnforcePure] attribute",
                    createChangedDocument: c => RemoveEnforcePureAttributeAsync(context.Document, methodDecl, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Document> RemoveEnforcePureAttributeAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document;

            var attributeToRemove = methodDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() == "EnforcePure");

            if (attributeToRemove == null)
                return document;

            var attributeList = (AttributeListSyntax)attributeToRemove.Parent!;

            var newMethodDecl = methodDecl;
            if (attributeList.Attributes.Count == 1)
            {

                newMethodDecl = methodDecl.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia)!;
            }
            else
            {

                if (attributeToRemove != null)
                {
                    var nullNode = default(SyntaxNode);
                    if (nullNode != null)
                    {
                        newMethodDecl = methodDecl.ReplaceNode(attributeToRemove, nullNode)!;
                    }
                    else
                    {



                    }
                }
            }

            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
