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

            if (context.Node is not AttributeListSyntax attributeList)
            {
                return;
            }

            var attributeTarget = attributeList.Parent;

            if (enforcePureAttributeSymbol != null)
            {
                var enforcePureAttributeLocation = FindAttributeLocation(attributeList, enforcePureAttributeSymbol, context.SemanticModel);
                if (enforcePureAttributeLocation != null && !IsAllowedPurityTarget(attributeTarget))
                {
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.MisplacedAttributeRule,
                        enforcePureAttributeLocation
                    );
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            if (pureAttributeSymbol != null)
            {
                var pureAttributeLocation = FindAttributeLocation(attributeList, pureAttributeSymbol, context.SemanticModel);
                if (pureAttributeLocation != null && !IsAllowedPureAttributeTarget(attributeTarget))
                {
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.MisplacedAttributeRule,
                        pureAttributeLocation
                    );
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }

            if (allowSynchronizationAttributeSymbol != null)
            {
                var allowSynchronizationAttributeLocation = FindAttributeLocation(attributeList, allowSynchronizationAttributeSymbol, context.SemanticModel);
                if (allowSynchronizationAttributeLocation != null && !IsAllowedPurityTarget(attributeTarget))
                {
                    var diag = Diagnostic.Create(
                        PurelySharpDiagnostics.MisplacedAllowSynchronizationAttributeRule,
                        allowSynchronizationAttributeLocation);
                    context.ReportDiagnostic(diag);
                    return;
                }
            }
        }

        private static Location? FindAttributeLocation(SyntaxList<AttributeListSyntax> attributeLists, INamedTypeSymbol targetAttributeSymbol, SemanticModel semanticModel)
        {
            foreach (var attributeList in attributeLists)
            {
                var location = FindAttributeLocation(attributeList, targetAttributeSymbol, semanticModel);
                if (location != null) return location;
            }
            return null;
        }

        private static Location? FindAttributeLocation(AttributeListSyntax attributeList, INamedTypeSymbol targetAttributeSymbol, SemanticModel semanticModel)
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
            return null;
        }

        private static bool IsAllowedPurityTarget(SyntaxNode? node)
        {
            return node is MethodDeclarationSyntax ||
                   node is AccessorDeclarationSyntax ||
                   node is ConstructorDeclarationSyntax ||
                   node is OperatorDeclarationSyntax ||
                   node is LocalFunctionStatementSyntax;
        }

        private static bool IsAllowedPureAttributeTarget(SyntaxNode? node)
        {
            return IsAllowedPurityTarget(node) ||
                   node is PropertyDeclarationSyntax ||
                   node is IndexerDeclarationSyntax;
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
