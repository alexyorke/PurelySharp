using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.AnalyzerStrategies
{
    /// <summary>
    /// Defines a strategy for performing a specific purity check on a syntax node (like MethodDeclaration, ConstructorDeclaration, etc.).
    /// </summary>
    public interface IPurityAnalyzerCheck
    {
        /// <summary>
        /// Performs a specific purity check on the given syntax node.
        /// </summary>
        /// <param name="node">The syntax node to check (e.g., MethodDeclarationSyntax, ConstructorDeclarationSyntax).</param>
        /// <param name="context">The syntax node analysis context.</param>
        /// <returns>A CheckPurityResult indicating whether the check passed and details if it failed.</returns>
        CheckPurityResult Check(CSharpSyntaxNode node, SyntaxNodeAnalysisContext context);
    }
} 