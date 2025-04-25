using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;
using System.Collections.Generic;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer
{
    internal static class MethodPurityAnalyzer
    {
        // Renamed and generalized the analysis method
        internal static void AnalyzeSymbolForPurity(SyntaxNodeAnalysisContext context)
        {
            // Get the symbol for the current syntax node.
            ISymbol? declaredSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken);

            // Ensure it's a method symbol (covers methods, accessors, operators, constructors, local functions)
            if (!(declaredSymbol is IMethodSymbol methodSymbol))
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
            if (allowSynchronizationAttributeSymbol == null)
            {
                // Optionally log a warning that the attribute wasn't found, but analysis can continue without it
                // System.Console.WriteLine("[PurelySharp Analyzer] Warning: Could not find AllowSynchronizationAttribute symbol.");
                // Depending on desired behavior, could return or proceed assuming it's not used.
                // Let's proceed, assuming locks will be marked impure if attribute isn't found.
            }

            bool hasEnforcePureAttribute = methodSymbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol));

            if (!hasEnforcePureAttribute)
            {
                return; // Only analyze symbols explicitly marked
            }

            // --- Check for implementation --- 
            // Avoid analyzing abstract/interface/extern methods without bodies here
            // (They are handled inside PurityAnalysisEngine)
            bool hasBody = methodSymbol.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax(context.CancellationToken))
                .Any(syntax => SyntaxHasBody(syntax));

            if (!hasBody && !methodSymbol.IsAbstract && !methodSymbol.IsExtern && methodSymbol.ContainingType.TypeKind != TypeKind.Interface)
            {
                // If no body is found for a concrete non-extern method, 
                // it might be an issue (e.g., partial method without implementation)
                // or an implicit member handled internally by PurityAnalysisEngine. Let the engine handle it.
                // LogDebug($"AnalyzeSymbolForPurity: No body found for concrete method {methodSymbol.ToDisplayString()}, deferring to PurityAnalysisEngine.");
            }
            else if (!hasBody)
            {
                // Don't analyze abstract/interface/extern methods here, they have no body to analyze with CFG anyway.
                // PurityAnalysisEngine handles these cases based on symbol properties.
                return;
            }

            // --- Perform Purity Analysis --- 
            // System.Console.WriteLine($"[PurelySharp Analyzer] Checking symbol with [EnforcePure]: {methodSymbol.ToDisplayString()}");

            // +++ Pass allowSynchronizationAttributeSymbol to IsConsideredPure +++
            // Note: We pass context.SemanticModel now, not the full context
            PurityAnalysisEngine.PurityAnalysisResult purityResult = PurityAnalysisEngine.IsConsideredPure(
                methodSymbol,
                context.SemanticModel, // Pass SemanticModel
                enforcePureAttributeSymbol,
                allowSynchronizationAttributeSymbol // Pass the loaded symbol (can be null)
                );
            bool isPure = purityResult.IsPure;

            // System.Console.WriteLine($"[PurelySharp Analyzer] Result for {methodSymbol.ToDisplayString()}: isPure = {isPure}");

            if (!isPure)
            {
                // Report the diagnostic on the identifier of the original syntax node
                SyntaxNode diagnosticNode = context.Node;
                Location? diagnosticLocation = GetIdentifierLocation(diagnosticNode);

                if (diagnosticLocation != null)
                {
                    // System.Console.WriteLine($"[PurelySharp Analyzer] Reporting {PurelySharpDiagnostics.PurityNotVerifiedRule.Id} for {methodSymbol.ToDisplayString()} at {diagnosticLocation.GetLineSpan()} because IsConsideredPure returned False.");

                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.PurityNotVerifiedRule,
                        diagnosticLocation,
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    // Fallback if identifier location couldn't be found
                    // System.Console.WriteLine($"[PurelySharp Analyzer] Reporting {PurelySharpDiagnostics.PurityNotVerifiedRule.Id} for {methodSymbol.ToDisplayString()} at node span {diagnosticNode.GetLocation().GetLineSpan()} (fallback).");
                    context.ReportDiagnostic(Diagnostic.Create(PurelySharpDiagnostics.PurityNotVerifiedRule, diagnosticNode.GetLocation(), methodSymbol.Name));
                }
            }
        }

        // Helper to check if a syntax node has a body or expression body
        private static bool SyntaxHasBody(SyntaxNode syntax)
        {
            return syntax switch
            {
                MethodDeclarationSyntax m => m.Body != null || m.ExpressionBody != null,
                AccessorDeclarationSyntax a => a.Body != null || a.ExpressionBody != null,
                ConstructorDeclarationSyntax c => c.Body != null || c.ExpressionBody != null,
                OperatorDeclarationSyntax o => o.Body != null || o.ExpressionBody != null,
                LocalFunctionStatementSyntax l => l.Body != null || l.ExpressionBody != null,
                _ => false
            };
        }

        // Helper to get the location of the identifier for various syntax kinds
        private static Location? GetIdentifierLocation(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax m => m.Identifier.GetLocation(),
                AccessorDeclarationSyntax a => a.Keyword.GetLocation(),
                ConstructorDeclarationSyntax c => c.Identifier.GetLocation(),
                OperatorDeclarationSyntax o => o.OperatorToken.GetLocation(),
                LocalFunctionStatementSyntax l => l.Identifier.GetLocation(),
                _ => node.GetLocation() // Fallback to the whole node span
            };
        }
    }
}