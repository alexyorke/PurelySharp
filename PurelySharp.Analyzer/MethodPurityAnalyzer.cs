using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;
using System.Collections.Generic; // For HashSet
using PurelySharp.Analyzer.Engine; // Added for PurityAnalysisEngine

namespace PurelySharp.Analyzer
{
    internal static class MethodPurityAnalyzer
    {
        internal static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Basic check: Does the method have an implementation body?
            bool hasImplementation = (methodDeclaration.Body != null && methodDeclaration.Body.Statements.Count > 0) ||
                                     methodDeclaration.ExpressionBody != null;

            if (!hasImplementation)
            {
                return; // Abstract, partial, extern methods without implementation are ignored for purity checks.
            }

            // Get the method symbol
            if (!(context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is IMethodSymbol methodSymbol))
            {
                return; // Could not get symbol
            }

            // Find the [EnforcePure] attribute symbol
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            if (enforcePureAttributeSymbol == null)
            {
                return; // Attribute not found in compilation
            }

            // --- Refactored Logic ---
            // Use the new engine for checks
            bool isPureEnforced = PurityAnalysisEngine.IsPureEnforced(methodSymbol, enforcePureAttributeSymbol);
            // Start recursive check with the method symbol and an empty visited set
            bool isConsideredPure = PurityAnalysisEngine.IsConsideredPure(methodSymbol, context, enforcePureAttributeSymbol, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));

            if (isPureEnforced)
            {
                // If attribute is present, method MUST be pure according to our checks.
                if (!isConsideredPure)
                {
                    // Report PS0002: Purity cannot be verified for [EnforcePure] method
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.PurityNotVerifiedRule,
                        methodDeclaration.Identifier.GetLocation(), // Location on method name
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
                // else: Pure and Enforced - Great! No diagnostic.
            }
            else // Attribute is NOT present
            {
                // If attribute is missing, but method LOOKS pure, suggest adding it.
                if (isConsideredPure)
                {
                    // Report PS0004: Method appears pure, suggest adding [EnforcePure]
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.MissingEnforcePureAttributeRule,
                        methodDeclaration.Identifier.GetLocation(), // Location on method name
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
                // else: Not pure and Not Enforced - Fine, no diagnostic.
            }
        }

        // REMOVED: Moved to PurityAnalysisEngine
        // private static bool IsConsideredPure(...) { ... }

        // REMOVED: Moved to PurityAnalysisEngine
        // private static bool IsExpressionPure(...) { ... }

        // REMOVED: Moved to PurityAnalysisEngine
        // private static bool IsPureEnforced(...) { ... }
    }
} 