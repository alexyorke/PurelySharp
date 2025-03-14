using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PureMethodAnalyzer
{
    public static class StatementPurityChecker
    {
        public static bool AreStatementsPure(SyntaxList<StatementSyntax> statements, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case LocalFunctionStatementSyntax localFunction:
                        // Local functions are pure if their body is pure
                        if (localFunction.Body != null)
                        {
                            if (!AreStatementsPure(localFunction.Body.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else if (localFunction.ExpressionBody != null)
                        {
                            if (!ExpressionPurityChecker.IsExpressionPure(localFunction.ExpressionBody.Expression, semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case LocalDeclarationStatementSyntax localDeclaration:
                        foreach (var variable in localDeclaration.Declaration.Variables)
                        {
                            if (variable.Initializer != null && !ExpressionPurityChecker.IsExpressionPure(variable.Initializer.Value, semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case ExpressionStatementSyntax expressionStatement:
                        if (!ExpressionPurityChecker.IsExpressionPure(expressionStatement.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case ReturnStatementSyntax returnStatement:
                        if (returnStatement.Expression != null && !ExpressionPurityChecker.IsExpressionPure(returnStatement.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case IfStatementSyntax ifStatement:
                        if (!ExpressionPurityChecker.IsExpressionPure(ifStatement.Condition, semanticModel, currentMethod))
                            return false;
                        if (ifStatement.Statement is BlockSyntax thenBlock)
                        {
                            if (!AreStatementsPure(thenBlock.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(ifStatement.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        if (ifStatement.Else != null)
                        {
                            if (ifStatement.Else.Statement is BlockSyntax elseBlock)
                            {
                                if (!AreStatementsPure(elseBlock.Statements, semanticModel, currentMethod))
                                    return false;
                            }
                            else
                            {
                                if (!AreStatementsPure(SyntaxFactory.SingletonList(ifStatement.Else.Statement), semanticModel, currentMethod))
                                    return false;
                            }
                        }
                        break;

                    case ForEachStatementSyntax forEach:
                        if (!ExpressionPurityChecker.IsExpressionPure(forEach.Expression, semanticModel, currentMethod))
                            return false;
                        if (forEach.Statement is BlockSyntax forEachBlock)
                        {
                            if (!AreStatementsPure(forEachBlock.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(forEach.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case ForStatementSyntax forStatement:
                        if (forStatement.Declaration != null)
                        {
                            foreach (var variable in forStatement.Declaration.Variables)
                            {
                                if (variable.Initializer != null && !ExpressionPurityChecker.IsExpressionPure(variable.Initializer.Value, semanticModel, currentMethod))
                                    return false;
                            }
                        }
                        if (forStatement.Condition != null && !ExpressionPurityChecker.IsExpressionPure(forStatement.Condition, semanticModel, currentMethod))
                            return false;
                        if (forStatement.Incrementors != null && !forStatement.Incrementors.All(inc => ExpressionPurityChecker.IsExpressionPure(inc, semanticModel, currentMethod)))
                            return false;
                        if (forStatement.Statement is BlockSyntax forBlock)
                        {
                            if (!AreStatementsPure(forBlock.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(forStatement.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case WhileStatementSyntax whileStatement:
                        if (!ExpressionPurityChecker.IsExpressionPure(whileStatement.Condition, semanticModel, currentMethod))
                            return false;
                        if (whileStatement.Statement is BlockSyntax whileBlock)
                        {
                            if (!AreStatementsPure(whileBlock.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(whileStatement.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case DoStatementSyntax doStatement:
                        if (!ExpressionPurityChecker.IsExpressionPure(doStatement.Condition, semanticModel, currentMethod))
                            return false;
                        if (doStatement.Statement is BlockSyntax doBlock)
                        {
                            if (!AreStatementsPure(doBlock.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        else
                        {
                            if (!AreStatementsPure(SyntaxFactory.SingletonList(doStatement.Statement), semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case SwitchStatementSyntax switchStatement:
                        if (!ExpressionPurityChecker.IsExpressionPure(switchStatement.Expression, semanticModel, currentMethod))
                            return false;
                        foreach (var section in switchStatement.Sections)
                        {
                            if (!AreStatementsPure(section.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        break;

                    case ThrowStatementSyntax throwStatement:
                        if (throwStatement.Expression != null && !ExpressionPurityChecker.IsExpressionPure(throwStatement.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case TryStatementSyntax tryStatement:
                        if (!AreStatementsPure(tryStatement.Block.Statements, semanticModel, currentMethod))
                            return false;
                        foreach (var catchClause in tryStatement.Catches)
                        {
                            if (!AreStatementsPure(catchClause.Block.Statements, semanticModel, currentMethod))
                                return false;
                        }
                        if (tryStatement.Finally != null && !AreStatementsPure(tryStatement.Finally.Block.Statements, semanticModel, currentMethod))
                            return false;
                        break;

                    case UsingStatementSyntax usingStatement:
                        // Using statements are impure by nature
                        return false;

                    case LockStatementSyntax lockStatement:
                        // Lock statements are impure by nature
                        return false;

                    case YieldStatementSyntax yieldStatement:
                        if (yieldStatement.Expression != null && !ExpressionPurityChecker.IsExpressionPure(yieldStatement.Expression, semanticModel, currentMethod))
                            return false;
                        break;

                    case BreakStatementSyntax:
                    case ContinueStatementSyntax:
                    case EmptyStatementSyntax:
                        break;

                    default:
                        // For any other statement type, assume it's impure
                        return false;
                }
            }
            return true;
        }
    }
}