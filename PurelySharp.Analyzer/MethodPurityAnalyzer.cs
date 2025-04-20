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
            // Example simple check: returning a constant literal
            if (IsConstantReturn(methodDeclaration))
            {
                return true;
            }

            // Future: Add more checks here (e.g., no static field writes, no I/O calls)
            // For now, any non-constant return method marked [EnforcePure] is flagged.
            return false;
        }

        private static bool IsConstantReturn(MethodDeclarationSyntax methodDeclaration)
        {
            // Check for expression body: => constant;
            if (methodDeclaration.ExpressionBody?.Expression is LiteralExpressionSyntax)
            {
                return true;
            }

            // Check for block body: { return constant; }
            if (methodDeclaration.Body?.Statements.Count == 1 &&
                methodDeclaration.Body.Statements[0] is ReturnStatementSyntax returnStatement &&
                returnStatement.Expression is LiteralExpressionSyntax)
            {
                return true;
            }

            return false;
        }

        private static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Check if the symbol has an attribute whose class matches the EnforcePure attribute symbol.
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 