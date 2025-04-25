using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity.
    /// </summary>
    internal static class PurityAnalysisEngine
    {
        // Add a set of known impure method signatures
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>
        {
            "System.IO.File.WriteAllText",
            "System.DateTime.Now.get", // Property getters are methods like get_Now
            // Add more known impure methods here
        };

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
                return false; // Reverted: Assume impure/unknown if no body analyzable
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
                else if (stmt is ExpressionStatementSyntax expressionStatement)
                {
                    // Check if it's an assignment expression
                    if (expressionStatement.Expression is AssignmentExpressionSyntax assignmentExpr)
                    {
                        // Check if the left side is a field (instance or static)
                        var leftSymbolInfo = context.SemanticModel.GetSymbolInfo(assignmentExpr.Left, context.CancellationToken);
                        if (leftSymbolInfo.Symbol is IFieldSymbol)
                        {
                            return false; // Assignment to a field is impure
                        }

                        // Check if the right side is pure. If not, the method is impure.
                        if (!IsExpressionPure(assignmentExpr.Right, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                        {
                            return false;
                        }
                        // Assignment to a local variable might be pure if the right side is pure.
                        // Need further checks depending on the left side (e.g., is it a local? Is it a property setter call?)
                        // For now, if it's not a field assignment and the right side is pure, we *could* continue,
                        // but the safest default is to assume impurity until proven otherwise.
                        // The current structure falls through to the final 'return false' if not explicitly handled.
                        // Let's assume assignments to non-fields are impure for now unless proven otherwise
                        // This path will eventually hit the `return false;` at the end of the loop's else block.

                        // If we reached here, it's an assignment, but not to a field, and the right side is pure.
                        // What about the left side? If it's a local variable, it's fine.
                        if (leftSymbolInfo.Symbol is ILocalSymbol)
                        {
                            // Assignment to a local variable with a pure expression is pure. Continue.
                            continue;
                        }
                        else
                        {
                            // Assignment to something else (e.g., property, indexer) - assume impure for now.
                            return false;
                        }
                    }
                    else
                    {
                        // An expression statement that isn't an assignment (e.g., method call like `list.Add(1)`)
                        // We need to evaluate the purity of the expression itself.
                        if (!IsExpressionPure(expressionStatement.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                        {
                            return false; // If the expression itself evaluates to impure (e.g., impure method call), the method is impure.
                        }
                        // If the expression is pure (e.g., calling a pure method), the statement itself is pure. Continue.
                    }
                }
                else if (stmt is IfStatementSyntax ifStatement)
                {
                    // 1. Check the condition's purity
                    if (!IsExpressionPure(ifStatement.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                    {
                        return false; // Impure condition
                    }

                    // 2. Check the 'if' block/statement's purity
                    if (!IsStatementPure(ifStatement.Statement, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, (IReadOnlyDictionary<ILocalSymbol, bool>)localPurityStatus))
                    {
                        return false; // Impure 'if' body
                    }

                    // 3. Check the 'else' block/statement's purity (if it exists)
                    if (ifStatement.Else != null && !IsStatementPure(ifStatement.Else.Statement, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, (IReadOnlyDictionary<ILocalSymbol, bool>)localPurityStatus))
                    {
                        return false; // Impure 'else' body
                    }

                    // If all checks pass, the 'if' statement is pure in this context. Continue checking next statement.
                    continue;
                }
                else
                {
                    // Any other statement type makes the method impure for now
                    return false;
                }
            }

            // If the loop completes, it means all statements were pure local declarations or pure expression statements.
            // This is valid for a void method or a method that returns implicitly (e.g., iterator block, async method - though not handled yet).
            // For a simple void method, this means purity is maintained.
            return containingMethodSymbol.ReturnsVoid; // Or potentially true if non-void but ends without return (e.g., throws) - needs more logic.
        }

        /// <summary>
        /// Checks if a single statement (potentially a block) is pure.
        /// Helper for handling nested statements like those in 'if'/'else'.
        /// </summary>
        private static bool IsStatementPure(StatementSyntax statement, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited, IMethodSymbol containingMethodSymbol, IReadOnlyDictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (statement is BlockSyntax block)
            {
                // Analyze the block. Need to handle locals defined within this block scope.
                // Create a mutable copy for this scope manually, ensuring the comparer is set.
                var blockLocalPurityStatus = new Dictionary<ILocalSymbol, bool>(SymbolEqualityComparer.Default);
                foreach (var kvp in localPurityStatus)
                {
                    blockLocalPurityStatus.Add(kvp.Key, kvp.Value);
                }
                return AnalyzeBlockBodyInternal(block, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, blockLocalPurityStatus, allowNestedReturn: false);
            }
            else if (statement is ExpressionStatementSyntax expressionStatement)
            {
                // Logic largely copied from AnalyzeBlockBody loop
                if (expressionStatement.Expression is AssignmentExpressionSyntax assignmentExpr)
                {
                    var leftSymbolInfo = context.SemanticModel.GetSymbolInfo(assignmentExpr.Left, context.CancellationToken);
                    if (leftSymbolInfo.Symbol is IFieldSymbol) return false; // Field assignment
                    if (!IsExpressionPure(assignmentExpr.Right, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false; // Impure RHS
                    if (leftSymbolInfo.Symbol is ILocalSymbol) return true; // Local assignment (pure RHS)
                    return false; // Assignment to anything else (property, indexer)
                }
                else
                {
                    // Other expression statement (e.g., method call)
                    return IsExpressionPure(expressionStatement.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                }
            }
            else if (statement is ReturnStatementSyntax returnStatement)
            {
                // A return statement is pure if its expression is pure.
                // Control flow complexity (like returning early from loops) will be handled by the analysis of loops themselves.
                // For simple blocks (like in if/else), a return is fine if the expression is pure.
                return IsExpressionPure(returnStatement.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                // Analysis for purity requires tracking locals. For a single statement check,
                // just ensure the initializer (if any) is pure. Tracking the local is handled by AnalyzeBlockBodyInternal if this is in a block.
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    if (variable.Initializer != null)
                    {
                        if (!IsExpressionPure(variable.Initializer.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                        {
                            return false; // Impure initializer
                        }
                    }
                }
                return true; // Declaration with pure initializer is fine.
            }
            else if (statement is IfStatementSyntax ifStatement) // Handle nested ifs
            {
                if (!IsExpressionPure(ifStatement.Condition, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false;
                if (!IsStatementPure(ifStatement.Statement, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false; // Recurse
                if (ifStatement.Else != null && !IsStatementPure(ifStatement.Else.Statement, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus)) return false; // Recurse
                return true;
            }
            // Add other simple, pure statements here if needed.
            // E.g., EmptyStatementSyntax is pure.
            else if (statement is EmptyStatementSyntax)
            {
                return true;
            }
            // Default: Unhandled statement type is considered impure.
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Internal implementation for analyzing a block body, allowing control over nested returns.
        /// Takes a mutable dictionary for local purity status within the block.
        /// </summary>
        private static bool AnalyzeBlockBodyInternal(
            BlockSyntax body,
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enforcePureAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<ILocalSymbol, bool> localPurityStatus, // Mutable dictionary for this scope
            bool allowNestedReturn) // Can this block end with a return?
        {
            var statements = body.Statements;
            for (int i = 0; i < statements.Count; i++)
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
                            // Analyze initializer using the current scope's purity status
                            isInitializerPure = IsExpressionPure(variable.Initializer.Value, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                            if (!isInitializerPure) return false;
                        }
                        // Add/update the local's purity in the *mutable* dictionary for this block
                        localPurityStatus[localSymbol] = isInitializerPure;
                    }
                }
                else if (stmt is ReturnStatementSyntax returnStatement)
                {
                    // Only pure if allowed (e.g., top-level block) AND it's the last statement AND expression is pure
                    if (allowNestedReturn && i == statements.Count - 1)
                    {
                        return IsExpressionPure(returnStatement.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                    }
                    else
                    {
                        return false; // Nested return or return not at end
                    }
                }
                // Delegate other statement types to the single statement checker
                else if (!IsStatementPure(stmt, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, (IReadOnlyDictionary<ILocalSymbol, bool>)localPurityStatus))
                {
                    return false; // Impure statement found
                }
                // If IsStatementPure handles 'if', 'expression', 'local decl', etc. and returns true, we continue the loop.
            }

            // If the loop completes without returning false, the block is pure up to this point.
            // If it's the top-level block of a void method, purity is maintained.
            // If it's a nested block, it's considered pure.
            // The final return value semantics are handled by the top-level caller (IsConsideredPure).
            return true;
        }

        /// <summary>
        /// Checks if a given expression is considered pure based on the current rules.
        /// </summary>
        internal static bool IsExpressionPure(ExpressionSyntax? expression, SyntaxNodeAnalysisContext context, INamedTypeSymbol enforcePureAttributeSymbol, HashSet<IMethodSymbol> visited, IMethodSymbol containingMethodSymbol, IReadOnlyDictionary<ILocalSymbol, bool> localPurityStatus)
        {
            if (expression == null)
            {
                // Null expression can occur in some syntax errors, treat as impure
                return false;
            }

            // Handle `nameof` which is always pure
            if (expression is InvocationExpressionSyntax nameofInvocation &&
                nameofInvocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            {
                return true;
            }

            var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (constantValue.HasValue)
            {
                return true; // Constants are pure
            }

            if (expression is InvocationExpressionSyntax invocationExpression)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol invokedMethodSymbol)
                {
                    // --- Check against known impure methods ---
                    // Use ToDisplayString for potentially generic methods
                    var methodDisplayString = invokedMethodSymbol.OriginalDefinition.ToDisplayString();
                    // Simple check based on common impure patterns
                    if (KnownImpureMethods.Contains(methodDisplayString) ||
                        methodDisplayString.StartsWith("System.Console.") || // Console I/O
                        methodDisplayString.StartsWith("System.IO.") || // File I/O
                        methodDisplayString.StartsWith("System.Net.") || // Networking
                        methodDisplayString.StartsWith("System.Threading.") || // Threading primitives
                        methodDisplayString.Contains("Random")) // Randomness
                    {
                        return false;
                    }

                    // --- Recursively check user-defined or other methods ---
                    // Note: Need to pass the *original* visited set for cycle detection.
                    // Backtracking (removing containingMethodSymbol) happens in the caller IsConsideredPure.
                    return IsConsideredPure(invokedMethodSymbol, context, enforcePureAttributeSymbol, visited);
                }
                else
                {
                    // Invocation of something not resolved to a method symbol (e.g., delegate)
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
                    // Reading a method parameter is considered pure, unless it's 'ref' or 'out'
                    return parameterSymbol.RefKind == RefKind.None || parameterSymbol.RefKind == RefKind.In || parameterSymbol.RefKind == RefKind.RefReadOnly;
                }
                else if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // Static readonly fields are pure.
                    if (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly)
                    {
                        return true;
                    }
                    /* // Instance readonly fields accessed via 'this' are pure. -- Temporarily commented out due to CS0184
                    if (!fieldSymbol.IsStatic && fieldSymbol.IsReadOnly && identifierName is ThisExpressionSyntax)
                    {
                        return true;
                    }
                    */
                    // Check if accessed via 'in' or 'ref readonly' parameter
                    var baseExprInfo = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken);
                    if (baseExprInfo.Symbol is IParameterSymbol paramSymbol &&
                       (paramSymbol.RefKind == RefKind.In || paramSymbol.RefKind == RefKind.RefReadOnly))
                    {
                        // Reading any field via such a parameter should be considered pure
                        return true;
                    }
                    // All other field accesses (instance non-readonly, or instance readonly not via this)
                    // are considered potentially impure by this specific check.
                    // Purity might be allowed by the later check for member access on in/ref readonly params.
                    return false;
                }
                else if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
                {
                    // Property access requires checking the getter method
                    if (propertySymbol.GetMethod != null)
                    {
                        // Check known impure getters first
                        var propertyGetterName = propertySymbol.ContainingType.ToDisplayString() + "." + propertySymbol.Name + ".get";
                        if (KnownImpureMethods.Contains(propertyGetterName))
                        {
                            return false; // Known impure property getter
                        }
                        return IsConsideredPure(propertySymbol.GetMethod, context, enforcePureAttributeSymbol, visited);
                    }
                    // Property without accessible getter is likely an error or complex scenario, assume impure.
                    return false;
                }
                // Accessing other kinds of identifiers (e.g., type names) is generally fine.
                // Consider if accessing `this` implicitly is okay. It usually is for reading members.
                else if (symbolInfo.Symbol is ITypeSymbol) // Accessing a type name is pure
                {
                    return true;
                }
                // Any other identifier access not explicitly handled is assumed impure
                return false;
            }
            else if (expression is LiteralExpressionSyntax) // Includes numeric, string, char, null, boolean literals
            {
                return true;
            }
            else if (expression is BinaryExpressionSyntax binaryExpression)
            {
                // Binary operation is pure if both operands are pure *considering the context*
                bool leftIsPure = IsExpressionPure(binaryExpression.Left, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
                bool rightIsPure = IsExpressionPure(binaryExpression.Right, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);

                // Special case: If an operand is member access on in/ref readonly param, it's okay even if IsExpressionPure returned false initially.
                if (!leftIsPure && binaryExpression.Left is MemberAccessExpressionSyntax leftMember)
                {
                    var leftBaseInfo = context.SemanticModel.GetSymbolInfo(leftMember.Expression, context.CancellationToken);
                    if (leftBaseInfo.Symbol is IParameterSymbol lParam && (lParam.RefKind == RefKind.In || lParam.RefKind == RefKind.RefReadOnly))
                    {
                        leftIsPure = true; // Override: Reading member via in/ref readonly is pure here
                    }
                }
                if (!rightIsPure && binaryExpression.Right is MemberAccessExpressionSyntax rightMember)
                {
                    var rightBaseInfo = context.SemanticModel.GetSymbolInfo(rightMember.Expression, context.CancellationToken);
                    if (rightBaseInfo.Symbol is IParameterSymbol rParam && (rParam.RefKind == RefKind.In || rParam.RefKind == RefKind.RefReadOnly))
                    {
                        rightIsPure = true; // Override: Reading member via in/ref readonly is pure here
                    }
                }

                return leftIsPure && rightIsPure;
            }
            else if (expression is PrefixUnaryExpressionSyntax unaryExpression)
            {
                // Unary operation is pure if the operand is pure
                // Exclude ++ and -- which modify state
                if (unaryExpression.Kind() == SyntaxKind.PreIncrementExpression ||
                    unaryExpression.Kind() == SyntaxKind.PreDecrementExpression)
                {
                    return false;
                }
                return IsExpressionPure(unaryExpression.Operand, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (expression is PostfixUnaryExpressionSyntax postfixUnary)
            {
                // Exclude ++ and -- which modify state
                if (postfixUnary.Kind() == SyntaxKind.PostIncrementExpression ||
                    postfixUnary.Kind() == SyntaxKind.PostDecrementExpression)
                {
                    return false;
                }
                // Other postfix unary ops? If any exist that are pure, handle here.
                return false; // Assume impure otherwise
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
            else if (expression is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.DefaultLiteralExpression)
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
            else if (expression is ParenthesizedExpressionSyntax parenExpr)
            {
                return IsExpressionPure(parenExpr.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                // Need to evaluate the symbol being accessed
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
                if (symbolInfo.Symbol is IFieldSymbol fieldSymbol)
                {
                    // Static readonly fields are pure.
                    if (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly)
                    {
                        return true;
                    }
                    /* // Instance readonly fields accessed via 'this' are pure. -- Temporarily commented out due to CS0184
                    if (!fieldSymbol.IsStatic && fieldSymbol.IsReadOnly && memberAccess.Expression is ThisExpressionSyntax)
                    {
                        return true;
                    }
                    */
                    // Check if accessed via 'in' or 'ref readonly' parameter
                    var baseExprInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken);
                    if (baseExprInfo.Symbol is IParameterSymbol paramSymbol &&
                       (paramSymbol.RefKind == RefKind.In || paramSymbol.RefKind == RefKind.RefReadOnly))
                    {
                        // Reading any field via such a parameter should be considered pure
                        return true;
                    }
                    // All other field accesses (instance non-readonly, or instance readonly not via this)
                    // are considered potentially impure by this specific check.
                    // Purity might be allowed by the later check for member access on in/ref readonly params.
                    return false;
                }
                else if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
                {
                    // Property access requires checking the getter method
                    if (propertySymbol.GetMethod != null)
                    {
                        // Check known impure getters first
                        var propertyGetterName = propertySymbol.ContainingType.ToDisplayString() + "." + propertySymbol.Name + ".get";
                        if (KnownImpureMethods.Contains(propertyGetterName))
                        {
                            return false; // Known impure property getter
                        }
                        return IsConsideredPure(propertySymbol.GetMethod, context, enforcePureAttributeSymbol, visited);
                    }
                    // Property without accessible getter is likely an error or complex scenario, assume impure.
                    return false;
                }
                else if (symbolInfo.Symbol is IMethodSymbol)
                {
                    // Accessing a method group name itself is pure
                    return true;
                }
                // Accessing other members (e.g., events, types nested within expression) - assume impure for now
                return false;
            }
            else if (expression is ObjectCreationExpressionSyntax objectCreation)
            {
                // Object creation might be pure if the constructor is pure and all arguments are pure
                var constructorSymbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation, context.CancellationToken);
                if (constructorSymbolInfo.Symbol is IMethodSymbol constructorSymbol)
                {
                    // Check constructor purity recursively
                    bool constructorIsPure = IsConsideredPure(constructorSymbol, context, enforcePureAttributeSymbol, visited);
                    if (!constructorIsPure) return false;

                    // Check argument purity
                    if (objectCreation.ArgumentList != null)
                    {
                        foreach (var arg in objectCreation.ArgumentList.Arguments)
                        {
                            if (!IsExpressionPure(arg.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus))
                            {
                                return false; // Impure argument
                            }
                        }
                    }
                    // Check initializer purity (if any)
                    if (objectCreation.Initializer != null)
                    {
                        foreach (var initExpr in objectCreation.Initializer.Expressions)
                        {
                            if (initExpr is AssignmentExpressionSyntax initAssign)
                            {
                                // Initializing properties/fields requires checking the assignment target (property setter/field) and value
                                // For now, assume initializers make it impure until we handle setters properly
                                return false; // TODO: Refine initializer check
                            }
                            else
                            {
                                return false; // Non-assignment initializer expression? Assume impure.
                            }
                        }
                    }

                    return true; // Constructor and arguments are pure
                }
                return false; // Could not resolve constructor
            }
            else if (expression is CastExpressionSyntax castExpr)
            {
                // Cast is pure if the inner expression is pure
                return IsExpressionPure(castExpr.Expression, context, enforcePureAttributeSymbol, visited, containingMethodSymbol, localPurityStatus);
            }
            else if (expression is TypeOfExpressionSyntax)
            {
                // typeof() is pure
                return true;
            }
            else if (expression is ThisExpressionSyntax)
            {
                // Accessing 'this' itself is pure (it's what you do with it that might not be)
                return true;
            }
            // ---> REMOVE Check HERE for member access on readable parameters
            /*
            if (expression is MemberAccessExpressionSyntax memberAccessOnParamCheck) {
                var baseExprInfo = context.SemanticModel.GetSymbolInfo(memberAccessOnParamCheck.Expression, context.CancellationToken);
                if (baseExprInfo.Symbol is IParameterSymbol paramSymbol &&
                   (paramSymbol.RefKind == RefKind.In || paramSymbol.RefKind == RefKind.RefReadOnly))
                    {
                        // Assume accessing members via in/ref readonly params is pure for now.
                        // TODO: This might be too broad; should ideally check the accessed member's purity.
                        return true;
                    }
            }
            */
            // TODO: Handle other expression types like ArrayCreationExpressionSyntax, Collection Expressions (C# 12+), etc.

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