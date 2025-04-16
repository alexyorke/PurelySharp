using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.AnalyzerStrategies
{
    /// <summary>
    /// Strategy to check for access to static (non-const) or volatile fields within a syntax node.
    /// </summary>
    public class StaticFieldAccessCheckStrategy : IPurityAnalyzerCheck
    {
        public CheckPurityResult Check(CSharpSyntaxNode node, SyntaxNodeAnalysisContext context)
        {
            Location? impurityLocation = null;
            string reason = string.Empty;
            bool isImpure = false;

            // Find all identifier names within the node
            foreach (var identifier in node.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // Check for static non-const fields (excluding 'var' which isn't a field access)
                    bool isStaticNonConst = fieldSymbol.IsStatic && !fieldSymbol.IsConst && !identifier.IsVar;
                    // Check for volatile fields
                    bool isVolatile = fieldSymbol.IsVolatile;

                    if (isStaticNonConst || isVolatile)
                    {
                        isImpure = true;
                        impurityLocation = identifier.GetLocation();
                        reason = isVolatile ? "Access to volatile field" : "Access to static non-const field";
                        break; // Found impurity, no need to check further
                    }
                }
            }

            if (isImpure)
            {
                return CheckPurityResult.Fail(impurityLocation ?? node.GetLocation(), reason);
            }
            else
            {
                return CheckPurityResult.Pass;
            }
        }
    }
} 