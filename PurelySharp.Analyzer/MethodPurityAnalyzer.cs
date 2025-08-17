using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;
using System.Collections.Generic;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer
{
    internal static class MethodPurityAnalyzer
    {
        // Renamed back
        internal static void AnalyzeSymbolForPurity(SyntaxNodeAnalysisContext context)
        {
            // Get the symbol for the current syntax node.
            ISymbol? declaredSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken);

            // Ensure it's a method symbol (covers methods, accessors, operators, constructors, local functions)
            if (!(declaredSymbol is IMethodSymbol methodSymbol))
            {
                return;
            }

            // Avoid analyzing symbols without locations (compiler generated) - Keep this check
            if (methodSymbol.Locations.FirstOrDefault() == null || methodSymbol.Locations.First().IsInMetadata)
            {
                return;
            }

            // --- Check for [EnforcePure] attribute --- 
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            var pureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);

            if (enforcePureAttributeSymbol == null && pureAttributeSymbol == null) // If neither attribute is found in compilation, can't proceed
            {
                return; // Cannot check without attribute symbols
            }

            // +++ Get [AllowSynchronization] attribute symbol +++
            var allowSynchronizationAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(AllowSynchronizationAttribute).FullName);
            // Null check is handled inside PurityAnalysisEngine

            bool hasPurityEnforcementAttribute = HasPurityEnforcement(methodSymbol, enforcePureAttributeSymbol, pureAttributeSymbol);

            // --- Perform Purity Analysis --- 
            // Create a new engine instance for each analysis
            var purityEngine = new PurityAnalysisEngine();
            // Pass enforcePureAttributeSymbol. PurityAnalysisEngine.IsPureEnforced now checks for both.
            // If enforcePureAttributeSymbol is null here, but pureAttributeSymbol was not, IsPureEnforced inside the engine will still correctly use pureAttributeSymbol for its checks.
            // Ensure we pass a non-null attribute symbol to the engine: prefer EnforcePure, fallback to Pure
            var enforceOrPureAttributeSymbol = GetEffectivePurityAttributeSymbol(enforcePureAttributeSymbol, pureAttributeSymbol);
            PurityAnalysisEngine.PurityAnalysisResult purityResult = purityEngine.IsConsideredPure(
                methodSymbol,
                context.SemanticModel,
                enforceOrPureAttributeSymbol,
                allowSynchronizationAttributeSymbol
                );
            bool isPure = purityResult.IsPure;

            // --- Report Diagnostic if Impure ---
            if (!isPure && hasPurityEnforcementAttribute) // Only report PS0002 if an attribute was present
            {
                // ALWAYS report on the method identifier if the method is impure
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
                    // This case should be rare if context.Node is always valid
                    PurityAnalysisEngine.LogDebug($"[MPA] Could not get identifier location for diagnostic on impure method {methodSymbol.Name}.");
                }
            }
            // +++ Report Diagnostic PS0004 if Pure but Missing Attribute +++
            else if (isPure && !hasPurityEnforcementAttribute) // Check against the combined flag
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
                    // If the method is determined to be pure BUT lacks the [EnforcePure] attribute, suggest adding it.
                    Location? diagnosticLocation = GetIdentifierLocation(context.Node);
                    PurityAnalysisEngine.LogDebug($"[MPA] Method '{methodSymbol.Name}' determined pure but lacks [EnforcePure]. Reporting PS0004 on identifier.");

                    if (diagnosticLocation != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            PurelySharpDiagnostics.MissingEnforcePureAttributeRule, // Use the PS0004 rule
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

        private static INamedTypeSymbol GetEffectivePurityAttributeSymbol(INamedTypeSymbol? enforcePureAttributeSymbol, INamedTypeSymbol? pureAttributeSymbol)
        {
            return enforcePureAttributeSymbol ?? pureAttributeSymbol!;
        }

        // REVERTED Helper: Get identifier location from SyntaxNode
        private static Location? GetIdentifierLocation(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax m => m.Identifier.GetLocation(),
                // Updated: Find parent Property/Indexer for Accessor identifier
                AccessorDeclarationSyntax a =>
                    a.Parent?.Parent switch
                    {
                        PropertyDeclarationSyntax p => p.Identifier.GetLocation(),
                        IndexerDeclarationSyntax i => i.ThisKeyword.GetLocation(), // Indexers use 'this'
                        _ => a.Keyword.GetLocation() // Fallback to keyword if parent isn't property/indexer
                    } ?? a.Keyword.GetLocation(), // Fallback if Parent?.Parent is null
                ConstructorDeclarationSyntax c => c.Identifier.GetLocation(),
                OperatorDeclarationSyntax o => o.OperatorToken.GetLocation(),
                LocalFunctionStatementSyntax l => l.Identifier.GetLocation(),
                // Fallback to the whole node span if it's none of the above (shouldn't happen often)
                _ => node.GetLocation()
            };
        }
    }
}