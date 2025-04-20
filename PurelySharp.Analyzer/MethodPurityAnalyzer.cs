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

            // --- Analysis --- 
            bool isPure = false; // Assume impure initially
            if (methodDeclaration.ExpressionBody != null)
            {
                // Case 1: Expression Body ( => ... )
                isPure = IsExpressionPure(methodDeclaration.ExpressionBody.Expression, context, enforcePureAttributeSymbol, visited, methodSymbol, new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default));
            }
            else if (methodDeclaration.Body != null)
            {
                // Case 2: Block Body ( { ... } )
                var statements = methodDeclaration.Body.Statements;
                // Track purity of local variables declared in this scope
                var localPurityStatus = new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);

                if (statements.Count == 0)
                {
                    // Empty body might be considered pure depending on return type (void is pure, non-void needs return)
                    isPure = methodSymbol.ReturnsVoid; // Consider void pure, others impure w/o return
                }
                else
                {
                    // Check statements before the last one
                    bool nonReturnStatementsPure = true;
                    for (int i = 0; i < statements.Count - 1; i++)
                    {
                        var stmt = statements[i];
                        if (stmt is LocalDeclarationStatementSyntax localDecl)
                        {
                            // Allow local declarations only if initializer is pure
                            foreach (var variable in localDecl.Declaration.Variables)
                            {
                                var localSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as ILocalSymbol;
                                if (localSymbol == null) continue; // Should not happen?

                                bool isInitializerPure = true; // Default to pure if no initializer
                                if (variable.Initializer != null)
                                {
                                    isInitializerPure = IsExpressionPure(variable.Initializer.Value, context, enforcePureAttributeSymbol, visited, methodSymbol, localPurityStatus); // Pass tracker
                                    if (!isInitializerPure)
                                    {
                                        nonReturnStatementsPure = false;
                                    }
                                }
                                localPurityStatus[localSymbol] = isInitializerPure;
                            }
                        }
                        else
                        {
                            // Any other statement type (assignment, invocation without assignment, if, loop, etc.) is impure for now.
                            nonReturnStatementsPure = false;
                        }

                        if (!nonReturnStatementsPure) break; // Exit loop early if impurity found
                    }

                    // Check the last statement
                    if (nonReturnStatementsPure && statements.Last() is ReturnStatementSyntax returnStatement)
                    {
                        // If all previous statements were allowed, check the purity of the return expression, considering locals
                        isPure = IsExpressionPure(returnStatement.Expression, context, enforcePureAttributeSymbol, visited, methodSymbol, localPurityStatus); // Pass tracker
                    }
                    // else: Last statement wasn't a return, or previous statements were impure.
                    // isPure remains false.
                }
            }
            // else: No body (should have been caught earlier)

            // --- Backtrack & Return ---
            visited.Remove(methodSymbol); // Crucial: Remove *after* all analysis for this method is done.
            return isPure;
        }

        /// <summary>
        /// Checks if a given expression is considered pure based on the current rules.
        /// </summary>
        /// <param name="expression">The expression syntax to analyze.</param>
        /// <param name="context">The analysis context.</param>
        /// <param name="enforcePureAttributeSymbol">Symbol for the [EnforcePure] attribute.</param>
        /// <param name="visited">Set of methods currently in the recursion stack (for cycle detection).</param>
        /// <param name="containingMethodSymbol">The symbol of the method containing this expression (used for backtracking in visited set).</param>
        /// <param name="localPurityStatus">Tracks the purity of local variables in the current scope.</param>
        /// <returns>True if the expression is considered pure, false otherwise.</returns>
        private static bool IsExpressionPure(ExpressionSyntax? expression, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited, IMethodSymbol containingMethodSymbol, IReadOnlyDictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (expression == null)
            {
                 return false; // Cannot analyze null expression
            }

            // a. Check for constant value
            var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (constantValue.HasValue)
            {
                return true; // Compile-time constant value found.
            }

            // b. Check if it's a call to another potentially pure method
            if (expression is InvocationExpressionSyntax invocationExpression)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                {
                    // Recursively check the invoked method
                    // Note: We pass the *same* visited set down for cycle detection.
                    // We need to remove the *containing* method symbol from visited *after* this recursive call returns,
                    // which is handled by the caller (IsConsideredPure).
                    return IsConsideredPure(invokedMethodSymbol, context, enforcePureAttributeSymbol, visited);
                }
                else
                {
                    // Could not resolve symbol or it's not a method (e.g., delegate invocation?)
                    return false;
                }
            }
            // c. Check if it's a reference to a known pure local variable
            else if (expression is IdentifierNameSyntax identifierName)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken);
                if (symbolInfo.Symbol is ILocalSymbol localSymbol)
                {
                    // Check the tracked status of this local variable
                    return localPurityStatus.TryGetValue(localSymbol, out bool isPure) && isPure;
                }
                 // Could be parameter, field, etc. - currently treated as impure
            }

            // d. Add other checks here (e.g., pure binary operations, known pure framework methods)
            // else...
            
            // If it's not a recognized pure construct, assume impure for now.
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