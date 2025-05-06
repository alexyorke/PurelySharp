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
            if (enforcePureAttributeSymbol == null)
            {
                return; // Cannot check without attribute symbol
            }

            // +++ Get [AllowSynchronization] attribute symbol +++
            var allowSynchronizationAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(AllowSynchronizationAttribute).FullName);
            // Null check is handled inside PurityAnalysisEngine

            bool hasEnforcePureAttribute = methodSymbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol));

            // --- Perform Purity Analysis --- 
            // Create a new engine instance for each analysis
            var purityEngine = new PurityAnalysisEngine();
            PurityAnalysisEngine.PurityAnalysisResult purityResult = purityEngine.IsConsideredPure( // Use instance method
                methodSymbol,
                context.SemanticModel, // Use context's SemanticModel
                enforcePureAttributeSymbol,
                allowSynchronizationAttributeSymbol // Pass the loaded symbol (can be null)
                );
            bool isPure = purityResult.IsPure;

            // --- Report Diagnostic if Impure ---
            if (!isPure)
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
            else if (isPure && !hasEnforcePureAttribute)
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

        // Helper to check if a node is part of a collection initializer
        private static bool IsNodeInsideCollectionInitializer(SyntaxNode? node, out ExpressionSyntax? creationExpression)
        {
            creationExpression = null;
            SyntaxNode? current = node?.Parent; // Start walking from the parent

            while (current != null)
            {
                if (current is InitializerExpressionSyntax initializer)
                {
                    // Check if the parent of the initializer is the creation expression
                    if (initializer.Parent is ObjectCreationExpressionSyntax objCreation)
                    {
                        creationExpression = objCreation;
                        return true;
                    }
                    // Potentially handle other initializer contexts if necessary
                }
                else if (current is CollectionExpressionSyntax collExpr)
                {
                    // If we hit a CollectionExpression directly while walking up from the impure node
                    creationExpression = collExpr;
                    return true;
                }
                // Stop if we hit a statement or declaration boundary before finding an initializer/collection expr
                if (current is StatementSyntax || current is MemberDeclarationSyntax || current is LocalFunctionStatementSyntax)
                {
                    return false;
                }
                current = current.Parent;
            }
            return false;
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