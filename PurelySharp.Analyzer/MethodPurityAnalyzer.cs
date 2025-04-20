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

            // Check if the method is marked with [EnforcePure]
            if (IsPureEnforced(methodSymbol, enforcePureAttributeSymbol))
            {
                // Perform initial simple purity checks
                if (IsConsideredPure(methodDeclaration, methodSymbol, context))
                {
                    return; // Passed basic purity checks, no diagnostic needed for now.
                }

                // If not considered pure by simple checks, report the 'not verified' diagnostic.
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.PurityNotVerifiedRule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodSymbol.Name // Pass the method name for the message
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsConsideredPure(MethodDeclarationSyntax methodDeclaration, IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context)
        {
            // Use semantic model to check for constant value, which is more robust than just checking for literals.
            ExpressionSyntax? returnExpression = GetReturnExpressionSyntax(methodDeclaration);

            if (returnExpression != null)
            {
                // Ensure we have a semantic model before trying to use it
                if (context.SemanticModel != null)
                {
                    var constantValue = context.SemanticModel.GetConstantValue(returnExpression, context.CancellationToken);
                    if (constantValue.HasValue)
                    {
                        return true; // Compile-time constant value found.
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
                methodDeclaration.Body.Statements[0] is ReturnStatementSyntax returnStatement)
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