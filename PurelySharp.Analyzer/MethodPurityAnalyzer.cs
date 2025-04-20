using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;
using System.Collections.Generic; // For HashSet

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
            // Start recursive check with the method symbol and an empty visited set
            bool isConsideredPure = IsConsideredPure(methodSymbol, context, enforcePureAttributeSymbol, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));

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

        // Refactored signature: Operates on IMethodSymbol, includes visited set for cycle detection
        private static bool IsConsideredPure(IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited)
        {
            // --- Cycle Detection ---
            // Use SymbolEqualityComparer for comparing symbols correctly
            if (!visited.Add(methodSymbol))
            {
                return false; // Cycle detected, assume impure
            }

            // --- Base Cases ---
            // 1. Is the method explicitly marked [EnforcePure]? (Already checked by caller in initial call, but good for recursion)
            // REMOVED: This check belongs in the caller (AnalyzeMethodDeclaration) to decide between PS0002/PS0004.
            //          We only want IsConsideredPure to return true if the *implementation* is pure.
            // if (IsPureEnforced(methodSymbol, enforcePureAttributeSymbol))
            // {
            //     return true;
            // }

            // 2. Find the method's declaration syntax (only handle MethodDeclarationSyntax for now)
            MethodDeclarationSyntax? methodDeclaration = null;
            foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax decl) 
                { 
                    methodDeclaration = decl;
                    break;
                }
                // TODO: Could potentially handle other syntax kinds like LocalFunctionStatementSyntax if needed
            }

            if (methodDeclaration == null || (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null))
            {
                // No implementation found or not a kind we analyze (e.g., abstract, partial)
                return false;
            }

            // --- Recursive Analysis ---
            ExpressionSyntax? returnExpression = GetReturnExpressionSyntax(methodDeclaration); // Use existing helper

            if (returnExpression != null)
            {
                // a. Check for constant return
                var constantValue = context.SemanticModel.GetConstantValue(returnExpression, context.CancellationToken);
                if (constantValue.HasValue)
                {
                    visited.Remove(methodSymbol); // Backtrack
                    return true; // Compile-time constant value found.
                }

                // b. Check if returning the result of a call to another method
                if (returnExpression is InvocationExpressionSyntax invocationExpression)
                {
                    var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken);
                    if (symbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                    {
                        // Recursively check the invoked method
                        bool result = IsConsideredPure(invokedMethodSymbol, context, enforcePureAttributeSymbol, visited);
                        visited.Remove(methodSymbol); // Backtrack
                        return result;
                    }
                    else
                    {
                        // Could not resolve symbol or it's not a method (e.g., delegate invocation?)
                        // Assume impure for now.
                        visited.Remove(methodSymbol); // Backtrack
                        return false;
                    }
                }
                // c. Add other checks for known impure constructs here if needed (e.g., specific method calls like DateTime.Now)
                //    Or, more generally, handle non-constant, non-invocation returns.
                else
                {
                    // If it's not a constant and not a method invocation we can recurse into,
                    // consider it impure based on current rules (e.g., field access, parameter return, `new`, etc.)
                    visited.Remove(methodSymbol); // Backtrack
                    return false;
                }
            }
            // else: Method has a body but doesn't fit the simple return patterns (e.g., multiple statements, assignments)
            // For now, consider these impure until more sophisticated analysis (like CFG) is added.

            visited.Remove(methodSymbol); // Backtrack: If we reach here, the method wasn't proven pure by current checks
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