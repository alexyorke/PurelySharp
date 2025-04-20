using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;

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
            bool isPureEnforced = IsPureEnforced(methodSymbol, enforcePureAttributeSymbol);
            bool isConsideredPure = IsConsideredPure(methodDeclaration, context, enforcePureAttributeSymbol); // Pass attribute symbol

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

        // Updated signature: Added enforcePureAttributeSymbol parameter
        private static bool IsConsideredPure(MethodDeclarationSyntax methodDeclaration, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Use semantic model to check for constant value, which is more robust than just checking for literals.
            ExpressionSyntax? returnExpression = GetReturnExpressionSyntax(methodDeclaration);

            if (returnExpression != null)
            {
                // 1. Check for constant return
                var constantValue = context.SemanticModel.GetConstantValue(returnExpression, context.CancellationToken);
                if (constantValue.HasValue)
                {
                    return true; // Compile-time constant value found.
                }

                // 2. Check if returning the result of a call to another [EnforcePure] method
                if (returnExpression is InvocationExpressionSyntax invocationExpression)
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken);
                    if (symbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                    {
                        // Reuse the IsPureEnforced helper
                        if (IsPureEnforced(invokedMethodSymbol, enforcePureAttributeSymbol))
                        {
                            return true; // Calling a known pure method.
                        }
                    }
                }
            }

            // Fallback or further checks can go here.
            // For now, if it's not a recognized constant, it's not considered pure by this simple check.
            return false;
        }

        // Helper to get the expression from either an expression body or a single return statement
        private static ExpressionSyntax? GetReturnExpressionSyntax(MethodDeclarationSyntax methodDeclaration)
        {
            // Check for expression body: => expression;
            if (methodDeclaration.ExpressionBody?.Expression != null)
            {
                return methodDeclaration.ExpressionBody.Expression;
            }

            // Check for block body: { return expression; }
            if (methodDeclaration.Body?.Statements.Count == 1 &&
                methodDeclaration.Body.Statements[0] is ReturnStatementSyntax returnStatement &&
                returnStatement.Expression != null) // Ensure the return statement actually has an expression
            {
                return returnStatement.Expression;
            }

            return null; // Not a simple return case we handle here
        }

        private static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Check if the symbol has an attribute whose class matches the EnforcePure attribute symbol.
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 