using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity.
    /// </summary>
    internal static class PurityAnalysisEngine
    {
        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation.
        /// </summary>
        internal static bool IsConsideredPure(IMethodSymbol methodSymbol, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited)
        {
            // --- Cycle Detection ---
            if (!visited.Add(methodSymbol))
            {
                return false; // Cycle detected, assume impure
            }

            // --- Find Declaration --- 
            MethodDeclarationSyntax? methodDeclaration = null;
            foreach (var syntaxRef in methodSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax decl)
                {
                    methodDeclaration = decl;
                    break;
                }
            }

            if (methodDeclaration == null || (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null))
            {
                // No implementation found or not a kind we analyze.
                // We need to remove from visited before returning false here.
                visited.Remove(methodSymbol); 
                return false;
            }

            // --- Analyze Body --- 
            bool isPure = false;
            if (methodDeclaration.ExpressionBody != null)
            {
                isPure = IsExpressionPure(methodDeclaration.ExpressionBody.Expression, context, enforcePureAttributeSymbol, visited, methodSymbol, new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default));
            }
            else if (methodDeclaration.Body != null)
            {
                var statements = methodDeclaration.Body.Statements;
                var localPurityStatus = new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);

                if (statements.Count == 0)
                {
                    isPure = methodSymbol.ReturnsVoid;
                }
                else
                {
                    bool nonReturnStatementsPure = true;
                    for (int i = 0; i < statements.Count - 1; i++)
                    {
                        var stmt = statements[i];
                        if (stmt is LocalDeclarationStatementSyntax localDecl)
                        {
                            foreach (var variable in localDecl.Declaration.Variables)
                            {
                                var localSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as ILocalSymbol;
                                if (localSymbol == null) continue;

                                bool isInitializerPure = true;
                                if (variable.Initializer != null)
                                {
                                    isInitializerPure = IsExpressionPure(variable.Initializer.Value, context, enforcePureAttributeSymbol, visited, methodSymbol, localPurityStatus);
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
                            nonReturnStatementsPure = false;
                        }
                        if (!nonReturnStatementsPure) break;
                    }

                    if (nonReturnStatementsPure && statements.Last() is ReturnStatementSyntax returnStatement)
                    {
                        isPure = IsExpressionPure(returnStatement.Expression, context, enforcePureAttributeSymbol, visited, methodSymbol, localPurityStatus);
                    }
                }
            }

            // --- Backtrack & Return ---
            visited.Remove(methodSymbol);
            return isPure;
        }

        /// <summary>
        /// Checks if a given expression is considered pure based on the current rules.
        /// </summary>
        internal static bool IsExpressionPure(ExpressionSyntax? expression, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited, IMethodSymbol containingMethodSymbol, IReadOnlyDictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (expression == null)
            {
                return false;
            }

            var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (constantValue.HasValue)
            {
                return true;
            }

            if (expression is InvocationExpressionSyntax invocationExpression)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                {
                    // Note: Need to pass the *original* visited set for cycle detection.
                    // Backtracking (removing containingMethodSymbol) happens in the caller IsConsideredPure.
                    return IsConsideredPure(invokedMethodSymbol, context, enforcePureAttributeSymbol, visited);
                }
                else
                {
                    return false;
                }
            }
            else if (expression is IdentifierNameSyntax identifierName)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken);
                if (symbolInfo.Symbol is ILocalSymbol localSymbol)
                {
                    return localPurityStatus.TryGetValue(localSymbol, out bool isPure) && isPure;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Checks if a symbol is marked with the [EnforcePure] attribute.
        /// </summary>
        internal static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            // Null check for safety
            if (enforcePureAttributeSymbol == null)
            {
                return false;
            }
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 