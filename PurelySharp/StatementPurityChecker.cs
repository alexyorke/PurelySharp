using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Collections.Generic;

namespace PurelySharp
{
    public static class StatementPurityChecker
    {
        public static bool AreStatementsPure(IEnumerable<StatementSyntax> statements, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            foreach (var statement in statements)
            {
                if (!IsStatementPure(statement, semanticModel, currentMethod))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsStatementPure(StatementSyntax statement, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            switch (statement)
            {
                case BlockSyntax block: // Handle blocks recursively
                    return AreStatementsPure(block.Statements, semanticModel, currentMethod);

                case ExpressionStatementSyntax exprStatement:
                    return currentMethod != null && ExpressionPurityChecker.IsExpressionPure(exprStatement.Expression, semanticModel, currentMethod);

                case ReturnStatementSyntax returnStatement:
                    return returnStatement.Expression == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(returnStatement.Expression, semanticModel, currentMethod));

                case LocalDeclarationStatementSyntax localDecl:
                    // Allow declarations if initialization is pure
                    return localDecl.Declaration.Variables.All(v =>
                        v.Initializer == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(v.Initializer.Value, semanticModel, currentMethod)));

                case IfStatementSyntax ifStatement:
                    return (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(ifStatement.Condition, semanticModel, currentMethod)) &&
                           IsStatementPure(ifStatement.Statement, semanticModel, currentMethod) &&
                           (ifStatement.Else == null || IsStatementPure(ifStatement.Else.Statement, semanticModel, currentMethod));

                case SwitchStatementSyntax switchStatement:
                    return (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(switchStatement.Expression, semanticModel, currentMethod)) &&
                           switchStatement.Sections.All(section => AreStatementsPure(section.Statements, semanticModel, currentMethod));

                case ForStatementSyntax forStatement:
                    return (forStatement.Declaration == null || forStatement.Declaration.Variables.All(v => v.Initializer == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(v.Initializer.Value, semanticModel, currentMethod)))) &&
                           (forStatement.Condition == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(forStatement.Condition, semanticModel, currentMethod))) &&
                           forStatement.Incrementors.All(inc => currentMethod != null && ExpressionPurityChecker.IsExpressionPure(inc, semanticModel, currentMethod)) &&
                           IsStatementPure(forStatement.Statement, semanticModel, currentMethod);

                case ForEachStatementSyntax forEachStatement:
                    // Check expression purity and body purity. Assume iteration itself is pure.
                    return (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(forEachStatement.Expression, semanticModel, currentMethod)) &&
                           IsStatementPure(forEachStatement.Statement, semanticModel, currentMethod);

                case WhileStatementSyntax whileStatement:
                    return (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(whileStatement.Condition, semanticModel, currentMethod)) &&
                           IsStatementPure(whileStatement.Statement, semanticModel, currentMethod);

                case DoStatementSyntax doStatement:
                    return IsStatementPure(doStatement.Statement, semanticModel, currentMethod) &&
                           (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(doStatement.Condition, semanticModel, currentMethod));

                case TryStatementSyntax tryStatement:
                    return IsStatementPure(tryStatement.Block, semanticModel, currentMethod) &&
                           tryStatement.Catches.All(c => c.Block == null || IsStatementPure(c.Block, semanticModel, currentMethod)) &&
                           (tryStatement.Finally == null || IsStatementPure(tryStatement.Finally.Block, semanticModel, currentMethod));

                case UsingStatementSyntax usingStatement:
                    // Allow using if declaration/expression is pure and body is pure
                    return (usingStatement.Declaration == null || usingStatement.Declaration.Variables.All(v => v.Initializer == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(v.Initializer.Value, semanticModel, currentMethod)))) &&
                           (usingStatement.Expression == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(usingStatement.Expression, semanticModel, currentMethod))) &&
                           IsStatementPure(usingStatement.Statement, semanticModel, currentMethod);

                case LockStatementSyntax lockStatement:
                    // Locks are generally impure unless specifically allowed
                    // Check if the method has [AllowSynchronization]
                    // This requires access to the currentMethod symbol, which might be null
                    bool allowSync = currentMethod?.GetAttributes().Any(attr => attr.AttributeClass?.Name.Contains("AllowSynchronization") ?? false) ?? false;
                    return allowSync && (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(lockStatement.Expression, semanticModel, currentMethod)) &&
                           IsStatementPure(lockStatement.Statement, semanticModel, currentMethod);

                case YieldStatementSyntax yieldStatement: // Yield return/break considered pure if expression is pure
                     return yieldStatement.Kind() == SyntaxKind.YieldBreakStatement ||
                           (yieldStatement.Expression != null && currentMethod != null && ExpressionPurityChecker.IsExpressionPure(yieldStatement.Expression, semanticModel, currentMethod));

                case CheckedStatementSyntax checkedStatement: // Check inner block
                    return IsStatementPure(checkedStatement.Block, semanticModel, currentMethod);

                case FixedStatementSyntax fixedStatement: // Fixed is unsafe, thus impure
                    return false;

                case UnsafeStatementSyntax unsafeStatement: // Unsafe code is impure
                    return false;

                case ThrowStatementSyntax throwStatement: // Throwing exceptions can be considered pure
                    return throwStatement.Expression == null || (currentMethod != null && ExpressionPurityChecker.IsExpressionPure(throwStatement.Expression, semanticModel, currentMethod));

                case EmptyStatementSyntax _: // Empty statements are pure
                case BreakStatementSyntax _: // Control flow is pure
                case ContinueStatementSyntax _: // Control flow is pure
                case GotoStatementSyntax _: // Control flow is pure (though discouraged)
                    // LabeledStatementSyntax doesn't introduce impurity itself, just check its statement
                    // Fall through to default case or handle explicitly if needed.
                    // For now, assuming labels themselves are pure.
                    return true; // Assuming the label itself is pure, the inner statement check removed

                // Handle LabeledStatement explicitly if needed, otherwise remove/adjust.
                // Example: 
                // case LabeledStatementSyntax labeledStmt: 
                //     return IsStatementPure(labeledStmt.Statement, semanticModel, currentMethod);

                default:
                    // Check if it's a LabeledStatement implicitly
                    if (statement is LabeledStatementSyntax labeledStatement) {
                         return IsStatementPure(labeledStatement.Statement, semanticModel, currentMethod);
                    }
                    // Unknown statement types considered impure by default
                    return false;
            }
        }

        private static bool IsPureDisposable(ITypeSymbol type)
        {
            // Check if the type implements IDisposable
            var disposableInterface = type.AllInterfaces.FirstOrDefault(i => i.Name == "IDisposable");
            if (disposableInterface == null)
                return false;

            // Get the Dispose method
            var disposeMethod = type.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
            if (disposeMethod == null)
                return false;

            // Check if the Dispose method is empty (pure)
            var syntaxRef = disposeMethod.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
                return false;

            if (syntaxRef.GetSyntax() is not MethodDeclarationSyntax methodSyntax || methodSyntax.Body == null)
                return false;

            // An empty Dispose method is considered pure
            return !methodSyntax.Body.Statements.Any();
        }

        private static bool HasAllowSynchronizationAttribute(IMethodSymbol methodSymbol)
        {
            // Debug output - uncomment for debugging
            foreach (var attr in methodSymbol.GetAttributes())
            {
                if (attr.AttributeClass != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Attribute: {attr.AttributeClass.Name}, Full: {attr.AttributeClass.ToDisplayString()}");
                }
            }

            return methodSymbol.GetAttributes().Any(attr =>
                // Direct name match (case insensitive)
                attr.AttributeClass?.Name.Equals("AllowSynchronizationAttribute", StringComparison.OrdinalIgnoreCase) == true ||
                attr.AttributeClass?.Name.Equals("AllowSynchronization", StringComparison.OrdinalIgnoreCase) == true ||
                // Full name ending match (case insensitive)
                (attr.AttributeClass != null && (
                    attr.AttributeClass.ToDisplayString().Equals("AllowSynchronizationAttribute", StringComparison.OrdinalIgnoreCase) ||
                    attr.AttributeClass.ToDisplayString().EndsWith(".AllowSynchronizationAttribute", StringComparison.OrdinalIgnoreCase) ||
                    attr.AttributeClass.ToDisplayString().EndsWith(".AllowSynchronization", StringComparison.OrdinalIgnoreCase)
                )));
        }
    }
}