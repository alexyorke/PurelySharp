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
                // Delegate to the new AnalyzeBlockBody method
                isPure = AnalyzeBlockBody(methodDeclaration.Body, context, enforcePureAttributeSymbol, visited, methodSymbol);
            }

            // --- Backtrack & Return ---
            visited.Remove(methodSymbol);
            return isPure;
        }

        /// <summary>
        /// Analyzes the purity of a method's block body.
        /// </summary>
        private static bool AnalyzeBlockBody(BlockSyntax body, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited, IMethodSymbol containingMethodSymbol)
        {
            var statements = body.Statements;
            var localPurityStatus = new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);

            for (int i = 0; i < statements.Count; i++)
            {
                var stmt = statements[i];

                if (stmt is LocalDeclarationStatementSyntax localDecl)
                {
                    foreach (var variable in localDecl.Declaration.Variables)
                    {
                        var localSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as ILocalSymbol;
                        if (localSymbol == null) continue;

                        bool isInitializerPure = true; // Assume pure if no initializer
                        if (variable.Initializer != null)
                        {
                            isInitializerPure = IsExpressionPure(variable.Initializer.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                            if (!isInitializerPure)
                            {
                                return false; // Impure initializer makes the whole method impure
                            }
                        }
                        localPurityStatus[localSymbol] = isInitializerPure;
                    }
                }
                else if (stmt is ReturnStatementSyntax returnStatement)
                {
                    // Check if this is the last statement
                    if (i == statements.Count - 1)
                    {
                        // Purity depends on the returned expression
                        return IsExpressionPure(returnStatement.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                    else
                    {
                        // Return statement before the end is considered impure for now
                        return false;
                    }
                }
                else
                {
                    // Any other statement type makes the method impure
                    return false;
                }
            }

            // If the loop completes, it means all statements were pure local declarations.
            // This is only valid for a void method.
            return containingMethodSymbol.ReturnsVoid;
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
                    // Check the known purity status of the local variable
                    return localPurityStatus.TryGetValue(localSymbol, out bool isPure) && isPure;
                }
                else if (symbolInfo.Symbol is IParameterSymbol parameterSymbol)
                {
                    // Reading a method parameter is considered pure
                    return true;
                }
            }
            else if (expression is BinaryExpressionSyntax binaryExpression)
            {
                // Binary operation is pure if both operands are pure
                return IsExpressionPure(binaryExpression.Left, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                       IsExpressionPure(binaryExpression.Right, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (expression is PrefixUnaryExpressionSyntax unaryExpression)
            {
                // Unary operation is pure if the operand is pure
                return IsExpressionPure(unaryExpression.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (expression is SizeOfExpressionSyntax)
            {
                // sizeof() is always pure
                return true;
            }
            else if (expression is DefaultExpressionSyntax)
            {
                // default is always pure
                return true;
            }
            else if (expression is LiteralExpressionSyntax literal && literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefaultLiteralExpression)
            {
                // default literal is always pure
                return true;
            }
            else if (expression is ConditionalExpressionSyntax conditionalExpression)
            {
                // Conditional ?: is pure if condition and both branches are pure
                return IsExpressionPure(conditionalExpression.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                       IsExpressionPure(conditionalExpression.WhenTrue, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus) &&
                       IsExpressionPure(conditionalExpression.WhenFalse, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            // TODO: Handle other expression types like MemberAccessExpressionSyntax, ObjectCreationExpressionSyntax etc.

            // If the expression type isn't explicitly handled as pure, assume it's impure
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