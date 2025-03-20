using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PurelySharp.Core
{
    /// <summary>
    /// Core interface for purity analysis.
    /// </summary>
    public interface IPurityAnalyzer
    {
        /// <summary>
        /// Analyzes a method for purity.
        /// </summary>
        /// <param name="methodDeclaration">The method to analyze.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="context">The analysis context.</param>
        /// <returns>The analysis result.</returns>
        PurityAnalysisResult Analyze(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, PurityContext context);
    }
}