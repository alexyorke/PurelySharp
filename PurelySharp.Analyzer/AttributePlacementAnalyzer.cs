using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes; // Assuming EnforcePureAttribute is needed here

namespace PurelySharp.Analyzer
{
    internal static class AttributePlacementAnalyzer
    {
        internal static void AnalyzeNonMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            var pureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);

            if (enforcePureAttributeSymbol == null && pureAttributeSymbol == null) // If neither attribute is relevant, exit
            {
                return;
            }

            Location? attributeLocation = null;

            // Check attribute lists on various declaration types
            if (context.Node is MemberDeclarationSyntax memberDecl)
            {
                // Check for EnforcePureAttribute
                if (enforcePureAttributeSymbol != null)
                {
                    attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                }
                // If not found, or if it was found but we want to check for PureAttribute anyway (e.g. if both are misplaced)
                // For identical behavior, if EnforcePure is misplaced, we report. If Pure is misplaced, we report.
                if (attributeLocation == null && pureAttributeSymbol != null)
                {
                    attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, pureAttributeSymbol, context.SemanticModel);
                }
            }
            // Add checks for other Syntax types if needed (e.g., parameters, type parameters)

            if (attributeLocation != null)
            {
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.MisplacedAttributeRule,
                    attributeLocation
                );
                context.ReportDiagnostic(diagnostic);
                // No return here, as a node might have multiple attributes or roles.
                // Let other analyzers run.
            }
        }

        private static Location? FindAttributeLocation(SyntaxList<AttributeListSyntax> attributeLists, INamedTypeSymbol targetAttributeSymbol, SemanticModel semanticModel)
        {
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                    // Check if the attribute constructor's containing type matches the target attribute symbol
                    if (symbolInfo.Symbol is IMethodSymbol attributeConstructorSymbol &&
                        SymbolEqualityComparer.Default.Equals(attributeConstructorSymbol.ContainingType, targetAttributeSymbol))
                    {
                        return attribute.GetLocation();
                    }
                    // Handle cases where the symbol info directly gives the attribute type (less common for attributes)
                    else if (symbolInfo.Symbol is INamedTypeSymbol directAttributeSymbol &&
                             SymbolEqualityComparer.Default.Equals(directAttributeSymbol, targetAttributeSymbol))
                    {
                        return attribute.GetLocation();
                    }
                    // Handle cases where the attribute is resolved to a symbol (e.g. via alias)
                    else if (semanticModel.GetTypeInfo(attribute).Type is INamedTypeSymbol attributeType &&
                           SymbolEqualityComparer.Default.Equals(attributeType, targetAttributeSymbol))
                    {
                        return attribute.GetLocation();
                    }
                }
            }
            return null;
        }
    }
}