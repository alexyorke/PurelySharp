using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer
{
    internal static class AttributePlacementAnalyzer
    {
        internal static void AnalyzeNonMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.EnforcePureAttribute");
            var pureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
            var allowSynchronizationAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.AllowSynchronizationAttribute");

            if (enforcePureAttributeSymbol == null && pureAttributeSymbol == null && allowSynchronizationAttributeSymbol == null)
            {
                return;
            }

            Location? attributeLocation = null;


            if (context.Node is MemberDeclarationSyntax memberDecl)
            {

                if (enforcePureAttributeSymbol != null)
                {
                    attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                }


                if (attributeLocation == null && pureAttributeSymbol != null)
                {
                    attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, pureAttributeSymbol, context.SemanticModel);
                }

                if (attributeLocation == null && allowSynchronizationAttributeSymbol != null)
                {
                    attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, allowSynchronizationAttributeSymbol, context.SemanticModel);
                    if (attributeLocation != null)
                    {
                        var diag = Diagnostic.Create(
                            PurelySharpDiagnostics.MisplacedAllowSynchronizationAttributeRule,
                            attributeLocation);
                        context.ReportDiagnostic(diag);
                        return;
                    }
                }
            }


            if (attributeLocation != null)
            {
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.MisplacedAttributeRule,
                    attributeLocation
                );
                context.ReportDiagnostic(diagnostic);


            }
        }

        private static Location? FindAttributeLocation(SyntaxList<AttributeListSyntax> attributeLists, INamedTypeSymbol targetAttributeSymbol, SemanticModel semanticModel)
        {
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);

                    if (symbolInfo.Symbol is IMethodSymbol attributeConstructorSymbol &&
                        SymbolEqualityComparer.Default.Equals(attributeConstructorSymbol.ContainingType, targetAttributeSymbol))
                    {
                        return attribute.GetLocation();
                    }

                    else if (symbolInfo.Symbol is INamedTypeSymbol directAttributeSymbol &&
                             SymbolEqualityComparer.Default.Equals(directAttributeSymbol, targetAttributeSymbol))
                    {
                        return attribute.GetLocation();
                    }

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