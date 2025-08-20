using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer
{
    internal static class MethodPurityAnalyzer
    {

        internal static void AnalyzeSymbolForPurity(SyntaxNodeAnalysisContext context, Engine.CompilationPurityService purityService)
        {

            ISymbol? declaredSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken);


            if (!(declaredSymbol is IMethodSymbol methodSymbol))
            {
                return;
            }


            if (methodSymbol.Locations.FirstOrDefault() == null || methodSymbol.Locations.First().IsInMetadata)
            {
                return;
            }


            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.EnforcePureAttribute");
            var pureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

            if (enforcePureAttributeSymbol == null && pureAttributeSymbol == null)
            {
                return;
            }


            var allowSynchronizationAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.AllowSynchronizationAttribute");

            bool hasEnforcePureAttribute = enforcePureAttributeSymbol != null && HasAttribute(methodSymbol, enforcePureAttributeSymbol);
            bool hasPureAttribute = pureAttributeSymbol != null && HasAttribute(methodSymbol, pureAttributeSymbol);

            if (hasEnforcePureAttribute && hasPureAttribute)
            {
                Location? conflictingDiagnosticLocation = GetIdentifierLocation(context.Node);
                if (conflictingDiagnosticLocation != null)
                {
                    var conflicting = Diagnostic.Create(
                        PurelySharpDiagnostics.ConflictingPurityAttributesRule,
                        conflictingDiagnosticLocation,
                        methodSymbol.Name);
                    context.ReportDiagnostic(conflicting);
                }
            }


            bool hasPurityEnforcementAttribute = HasPurityEnforcement(methodSymbol, enforcePureAttributeSymbol, pureAttributeSymbol);


            var enforceOrPureAttributeSymbol = GetEffectivePurityAttributeSymbol(enforcePureAttributeSymbol, pureAttributeSymbol);
            PurityAnalysisEngine.PurityAnalysisResult purityResult = purityService.GetPurity(
                methodSymbol,
                context.SemanticModel,
                enforceOrPureAttributeSymbol,
                allowSynchronizationAttributeSymbol);
            bool isPure = purityResult.IsPure;


            if (!isPure && hasPurityEnforcementAttribute)
            {

                Location? diagnosticLocation = GetIdentifierLocation(context.Node);
                PurityAnalysisEngine.LogDebug($"[MPA] Method '{methodSymbol.Name}' determined impure. Reporting PS0002 on identifier.");

                if (diagnosticLocation != null)
                {
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.PurityNotVerifiedRule,
                        diagnosticLocation,
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                    PurityAnalysisEngine.LogDebug($"[MPA] Reported diagnostic PS0002 for {methodSymbol.Name} at {diagnosticLocation}.");
                }
                else
                {

                    PurityAnalysisEngine.LogDebug($"[MPA] Could not get identifier location for diagnostic on impure method {methodSymbol.Name}.");
                }
            }

            else if (isPure && !hasPurityEnforcementAttribute)
            {
                bool isCompilerGeneratedSetter = false;
                if (methodSymbol.MethodKind == MethodKind.PropertySet && context.Node is AccessorDeclarationSyntax setterNode)
                {
                    if (setterNode.Body == null && setterNode.ExpressionBody == null)
                    {
                        isCompilerGeneratedSetter = true;
                        PurityAnalysisEngine.LogDebug($"[MPA] Method '{methodSymbol.Name}' is an auto-property setter. Not a candidate for PS0004.");
                    }
                }

                if (!isCompilerGeneratedSetter)
                {

                    Location? diagnosticLocation = GetIdentifierLocation(context.Node);
                    PurityAnalysisEngine.LogDebug($"[MPA] Method '{methodSymbol.Name}' determined pure but lacks [EnforcePure]. Reporting PS0004 on identifier.");

                    if (diagnosticLocation != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            PurelySharpDiagnostics.MissingEnforcePureAttributeRule,
                            diagnosticLocation,
                            methodSymbol.Name
                        );
                        context.ReportDiagnostic(diagnostic);
                        PurityAnalysisEngine.LogDebug($"[MPA] Reported diagnostic PS0004 for {methodSymbol.Name} at {diagnosticLocation}.");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"[MPA] Could not get identifier location for diagnostic PS0004 on pure method {methodSymbol.Name}.");
                    }
                }
            }
        }

        private static bool HasPurityEnforcement(IMethodSymbol methodSymbol, INamedTypeSymbol? enforcePureAttributeSymbol, INamedTypeSymbol? pureAttributeSymbol)
        {
            foreach (var attributeData in methodSymbol.GetAttributes())
            {
                var attributeClass = attributeData.AttributeClass?.OriginalDefinition;
                if (enforcePureAttributeSymbol != null && SymbolEqualityComparer.Default.Equals(attributeClass, enforcePureAttributeSymbol))
                {
                    return true;
                }
                if (pureAttributeSymbol != null && SymbolEqualityComparer.Default.Equals(attributeClass, pureAttributeSymbol))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasAttribute(IMethodSymbol methodSymbol, INamedTypeSymbol attributeType)
        {
            foreach (var attributeData in methodSymbol.GetAttributes())
            {
                var attributeClass = attributeData.AttributeClass?.OriginalDefinition;
                if (SymbolEqualityComparer.Default.Equals(attributeClass, attributeType))
                {
                    return true;
                }
            }
            return false;
        }

        private static INamedTypeSymbol GetEffectivePurityAttributeSymbol(INamedTypeSymbol? enforcePureAttributeSymbol, INamedTypeSymbol? pureAttributeSymbol)
        {
            return enforcePureAttributeSymbol ?? pureAttributeSymbol!;
        }


        private static Location? GetIdentifierLocation(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax m => m.Identifier.GetLocation(),

                AccessorDeclarationSyntax a =>
                    a.Parent?.Parent switch
                    {
                        PropertyDeclarationSyntax p => p.Identifier.GetLocation(),
                        IndexerDeclarationSyntax i => i.ThisKeyword.GetLocation(),
                        _ => a.Keyword.GetLocation()
                    } ?? a.Keyword.GetLocation(),
                ConstructorDeclarationSyntax c => c.Identifier.GetLocation(),
                OperatorDeclarationSyntax o => o.OperatorToken.GetLocation(),
                LocalFunctionStatementSyntax l => l.Identifier.GetLocation(),

                _ => node.GetLocation()
            };
        }
    }
}