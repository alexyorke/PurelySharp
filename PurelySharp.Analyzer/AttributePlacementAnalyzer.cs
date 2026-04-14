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
            var enforcePureAttributeSymbol = ResolveAttributeSymbol(context.SemanticModel.Compilation, "PurelySharp.Attributes.EnforcePureAttribute", "EnforcePureAttribute");
            var pureAttributeSymbol = ResolveAttributeSymbol(context.SemanticModel.Compilation, "PurelySharp.Attributes.PureAttribute", "PureAttribute");
            var allowSynchronizationAttributeSymbol = ResolveAttributeSymbol(context.SemanticModel.Compilation, "PurelySharp.Attributes.AllowSynchronizationAttribute", "AllowSynchronizationAttribute");

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

        private static INamedTypeSymbol? ResolveAttributeSymbol(Compilation compilation, string qualifiedMetadataName, string fallbackMetadataName)
        {
            return compilation.GetTypeByMetadataName(qualifiedMetadataName)
                ?? compilation.GetTypeByMetadataName(fallbackMetadataName)
                ?? FindTypeByName(compilation.Assembly.GlobalNamespace, fallbackMetadataName);
        }

        private static INamedTypeSymbol? FindTypeByName(INamespaceSymbol namespaceSymbol, string typeName)
        {
            var directMatch = namespaceSymbol.GetTypeMembers(typeName).FirstOrDefault();
            if (directMatch != null)
            {
                return directMatch;
            }

            foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                var nestedMatch = FindTypeByName(nestedNamespace, typeName);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }
    }
}
