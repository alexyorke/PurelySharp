using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PureMethodAnalyzer
{
    public static class ExpressionPurityChecker
    {
        public static bool IsExpressionPure(ExpressionSyntax? expression, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            if (expression == null) return true;

            switch (expression)
            {
                case LiteralExpressionSyntax:
                    return true;

                case IdentifierNameSyntax identifier:
                    var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol != null)
                    {
                        if (symbol.Kind == SymbolKind.Parameter)
                        {
                            var parameter = symbol as IParameterSymbol;
                            // Check if parameter is ref/out
                            if (parameter?.RefKind != RefKind.None)
                                return false;
                            return true;
                        }
                        if (symbol is IFieldSymbol fieldSymbol)
                        {
                            // Static fields that are not const are impure
                            if (fieldSymbol.IsStatic && !fieldSymbol.IsConst)
                                return false;
                        }
                    }
                    return symbol != null && SymbolPurityChecker.IsPureSymbol(symbol);

                case InvocationExpressionSyntax invocation:
                    var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                        return false;

                    // Handle recursive calls
                    if (currentMethod != null && SymbolEqualityComparer.Default.Equals(methodSymbol, currentMethod))
                    {
                        // Check if recursive call is in a pure context
                        return invocation.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));
                    }

                    // Check if it's a known pure method (LINQ, Math, etc.)
                    if (MethodPurityChecker.IsKnownPureMethod(methodSymbol))
                        return invocation.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                    // Check if it's marked with [EnforcePure]
                    bool isCalledMethodPure = methodSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.Name == "EnforcePureAttribute");

                    // Check if it's an interface method
                    if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface)
                    {
                        // Interface methods are considered pure if they are getters or have no side effects
                        if (methodSymbol.MethodKind == MethodKind.PropertyGet)
                            return true;
                        if (methodSymbol.Name == "Convert" || methodSymbol.Name == "CompareTo" || methodSymbol.Name == "Equals")
                            return true;
                    }

                    return isCalledMethodPure &&
                           invocation.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                case MemberAccessExpressionSyntax memberAccess:
                    var memberSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (memberSymbol == null)
                        return false;

                    // Handle string methods
                    if (memberSymbol is IMethodSymbol stringMethod &&
                        stringMethod.ContainingType.SpecialType == SpecialType.System_String)
                    {
                        return true; // All string methods are pure
                    }

                    // Handle Math constants and methods
                    if (memberSymbol.ContainingType?.Name == "Math" &&
                        NamespaceChecker.IsInNamespace(memberSymbol.ContainingType, "System"))
                    {
                        return true; // All Math members are pure
                    }

                    // Handle LINQ extension methods
                    if (memberSymbol is IMethodSymbol linqMethod &&
                        NamespaceChecker.IsInNamespace(linqMethod.ContainingType, "System.Linq") &&
                        linqMethod.ContainingType.Name == "Enumerable")
                    {
                        return true; // All LINQ methods are pure
                    }

                    // Handle interface property getters
                    if (memberSymbol is IPropertySymbol property &&
                        property.ContainingType.TypeKind == TypeKind.Interface &&
                        property.GetMethod != null && property.SetMethod == null)
                    {
                        return true; // Interface read-only properties are pure
                    }

                    // Handle other member access
                    return SymbolPurityChecker.IsPureSymbol(memberSymbol) &&
                           (memberAccess.Expression == null || IsExpressionPure(memberAccess.Expression, semanticModel, currentMethod));

                // Add support for switch expressions (C# 8.0+)
                case SwitchExpressionSyntax switchExpr:
                    // Check the governing expression
                    if (!IsExpressionPure(switchExpr.GoverningExpression, semanticModel, currentMethod))
                        return false;

                    // Check all arms
                    foreach (var arm in switchExpr.Arms)
                    {
                        // Patterns themselves are pure, we only need to check when clauses and result expressions
                        if (arm.WhenClause != null && !IsExpressionPure(arm.WhenClause.Condition, semanticModel, currentMethod))
                            return false;

                        if (!IsExpressionPure(arm.Expression, semanticModel, currentMethod))
                            return false;
                    }
                    return true;

                case ObjectCreationExpressionSyntax objectCreation:
                    var typeSymbol = semanticModel.GetSymbolInfo(objectCreation.Type).Symbol as ITypeSymbol;
                    if (typeSymbol == null)
                        return false;

                    // Check if it's a known impure type
                    var impureTypes = new[] {
                        "Random", "FileStream", "StreamWriter", "StreamReader",
                        "StringBuilder", "Task", "Thread", "Timer",
                        "WebClient", "HttpClient", "Socket", "NetworkStream"
                    };
                    if (impureTypes.Contains(typeSymbol.Name) || NamespaceChecker.IsInImpureNamespace(typeSymbol))
                        return false;

                    // Check if the type is a collection that could be modified
                    if (CollectionChecker.IsModifiableCollectionType(typeSymbol))
                        return false;

                    return objectCreation.ArgumentList?.Arguments.All(arg =>
                        IsExpressionPure(arg.Expression, semanticModel, currentMethod)) ?? true;

                case TupleExpressionSyntax tuple:
                    return tuple.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                case DeclarationExpressionSyntax declaration:
                    // Tuple deconstruction is pure
                    if (declaration.Designation is ParenthesizedVariableDesignationSyntax parenthesized)
                    {
                        return parenthesized.Variables.All(v => v is SingleVariableDesignationSyntax);
                    }
                    return declaration.Designation is SingleVariableDesignationSyntax;

                case BinaryExpressionSyntax binary:
                    if (binary.IsKind(SyntaxKind.CoalesceExpression))
                    {
                        return IsExpressionPure(binary.Left, semanticModel, currentMethod) &&
                               IsExpressionPure(binary.Right, semanticModel, currentMethod);
                    }
                    return IsExpressionPure(binary.Left, semanticModel, currentMethod) &&
                           IsExpressionPure(binary.Right, semanticModel, currentMethod);

                case ParenthesizedExpressionSyntax paren:
                    return IsExpressionPure(paren.Expression, semanticModel, currentMethod);

                case ConditionalExpressionSyntax conditional:
                    return IsExpressionPure(conditional.Condition, semanticModel, currentMethod) &&
                           IsExpressionPure(conditional.WhenTrue, semanticModel, currentMethod) &&
                           IsExpressionPure(conditional.WhenFalse, semanticModel, currentMethod);

                case PrefixUnaryExpressionSyntax prefix:
                    return IsExpressionPure(prefix.Operand, semanticModel, currentMethod);

                case PostfixUnaryExpressionSyntax postfix:
                    return IsExpressionPure(postfix.Operand, semanticModel, currentMethod);

                case SimpleLambdaExpressionSyntax lambda:
                    var lambdaDataFlow = semanticModel.AnalyzeDataFlow(lambda.Body);
                    if (!lambdaDataFlow.Succeeded || DataFlowChecker.HasImpureCaptures(lambdaDataFlow))
                        return false;
                    return IsExpressionPure(lambda.Body as ExpressionSyntax, semanticModel, currentMethod);

                case ParenthesizedLambdaExpressionSyntax lambda:
                    var lambdaBlockDataFlow = semanticModel.AnalyzeDataFlow(lambda.Body);
                    if (!lambdaBlockDataFlow.Succeeded || DataFlowChecker.HasImpureCaptures(lambdaBlockDataFlow))
                        return false;
                    if (lambda.Body is ExpressionSyntax expr)
                        return IsExpressionPure(expr, semanticModel, currentMethod);
                    if (lambda.Body is BlockSyntax block)
                        return StatementPurityChecker.AreStatementsPure(block.Statements, semanticModel, currentMethod);
                    return false;

                case ElementAccessExpressionSyntax elementAccessExpr:
                    return IsExpressionPure(elementAccessExpr.Expression, semanticModel, currentMethod) &&
                           elementAccessExpr.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                case AssignmentExpressionSyntax assignment:
                    // Handle tuple deconstruction
                    if (assignment.Left is TupleExpressionSyntax tupleLeft)
                    {
                        return tupleLeft.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod)) &&
                               IsExpressionPure(assignment.Right, semanticModel, currentMethod);
                    }
                    // Handle array/span element assignment
                    if (assignment.Left is ElementAccessExpressionSyntax elementAccess)
                    {
                        return IsExpressionPure(elementAccess.Expression, semanticModel, currentMethod) &&
                               elementAccess.ArgumentList.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod)) &&
                               IsExpressionPure(assignment.Right, semanticModel, currentMethod);
                    }
                    // Handle variable declaration with tuple deconstruction
                    if (assignment.Left is DeclarationExpressionSyntax decl)
                    {
                        return IsExpressionPure(decl, semanticModel, currentMethod) &&
                               IsExpressionPure(assignment.Right, semanticModel, currentMethod);
                    }
                    return false;

                case StackAllocArrayCreationExpressionSyntax stackAlloc:
                    return stackAlloc.Initializer == null ||
                           stackAlloc.Initializer.Expressions.All(expr => IsExpressionPure(expr, semanticModel, currentMethod));

                case InterpolatedStringExpressionSyntax interpolatedString:
                    return interpolatedString.Contents.OfType<InterpolationSyntax>()
                        .All(interp => IsExpressionPure(interp.Expression, semanticModel, currentMethod));

                case ConditionalAccessExpressionSyntax conditionalAccess:
                    return IsExpressionPure(conditionalAccess.Expression, semanticModel, currentMethod) &&
                           IsExpressionPure(conditionalAccess.WhenNotNull, semanticModel, currentMethod);

                case AnonymousObjectCreationExpressionSyntax anonymousObject:
                    return anonymousObject.Initializers.All(init => IsExpressionPure(init.Expression, semanticModel, currentMethod));

                case QueryExpressionSyntax query:
                    // LINQ query expressions are pure if their source and operations are pure
                    if (!IsExpressionPure(query.FromClause.Expression, semanticModel, currentMethod))
                        return false;

                    // Check if the select clause is pure
                    if (query.Body.SelectOrGroup is SelectClauseSyntax select)
                    {
                        if (!IsExpressionPure(select.Expression, semanticModel, currentMethod))
                            return false;
                    }
                    else if (query.Body.SelectOrGroup is GroupClauseSyntax group)
                    {
                        if (!IsExpressionPure(group.GroupExpression, semanticModel, currentMethod) ||
                            !IsExpressionPure(group.ByExpression, semanticModel, currentMethod))
                            return false;
                    }

                    // Check if all other clauses are pure
                    foreach (var clause in query.Body.Clauses)
                    {
                        if (clause is WhereClauseSyntax where)
                        {
                            if (!IsExpressionPure(where.Condition, semanticModel, currentMethod))
                                return false;
                        }
                        else if (clause is OrderByClauseSyntax orderBy)
                        {
                            if (!orderBy.Orderings.All(o => IsExpressionPure(o.Expression, semanticModel, currentMethod)))
                                return false;
                        }
                        else if (clause is JoinClauseSyntax join)
                        {
                            if (!IsExpressionPure(join.InExpression, semanticModel, currentMethod) ||
                                !IsExpressionPure(join.LeftExpression, semanticModel, currentMethod) ||
                                !IsExpressionPure(join.RightExpression, semanticModel, currentMethod))
                                return false;
                        }
                        else if (clause is FromClauseSyntax from)
                        {
                            if (!IsExpressionPure(from.Expression, semanticModel, currentMethod))
                                return false;
                        }
                        else if (clause is LetClauseSyntax let)
                        {
                            if (!IsExpressionPure(let.Expression, semanticModel, currentMethod))
                                return false;
                        }
                    }
                    return true;

                default:
                    return false;
            }
        }
    }
}