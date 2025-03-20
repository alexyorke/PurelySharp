using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PurelySharp
{
    public static class ExpressionPurityChecker
    {
        public static bool IsExpressionPure(ExpressionSyntax? expression, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            if (expression == null) return true;

            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    // All literals including raw string literals are pure
                    return true;

                case IdentifierNameSyntax identifier:
                    var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol != null)
                    {
                        if (symbol is IParameterSymbol parameter)
                        {
                            // Check if parameter is ref/out
                            if (parameter.RefKind != RefKind.None)
                                return false;
                            return true;
                        }
                        if (symbol is IFieldSymbol fieldSymbolIdentifier)
                        {
                            // Static fields that are not const are impure
                            if (fieldSymbolIdentifier.IsStatic && !fieldSymbolIdentifier.IsConst)
                                return false;
                        }
                    }
                    return symbol != null && SymbolPurityChecker.IsPureSymbol(symbol);

                case InvocationExpressionSyntax invocation:
                    // Check the method being called
                    var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                        return false;

                    // Check for Console methods which are impure
                    if (methodSymbol.ContainingType?.Name == "Console" &&
                        methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
                    {
                        if (methodSymbol.Name is "WriteLine" or "Write" or "ReadLine" or "Read" or "ReadKey")
                            return false;
                    }

                    // Check for other known impure namespaces
                    var containingNamespace = methodSymbol.ContainingNamespace?.ToString() ?? string.Empty;
                    if (containingNamespace.StartsWith("System.IO") ||
                        containingNamespace.StartsWith("System.Net") ||
                        containingNamespace.StartsWith("System.Web") ||
                        containingNamespace.StartsWith("System.Threading"))
                    {
                        return false;
                    }

                    // Check if this is a recursive call to the current method
                    if (currentMethod != null && methodSymbol.Equals(currentMethod, SymbolEqualityComparer.Default))
                    {
                        // Recursive calls are pure if the current method is pure
                        // The analyzer will handle this case
                        return true;
                    }

                    // Known pure methods (LINQ, Math, etc.)
                    if (IsKnownPureMethod(methodSymbol))
                        return true;

                    // Check if method has EnforcePure attribute
                    if (HasEnforcePureAttribute(methodSymbol))
                        return true;

                    // Interface method that are property getters or have no side effects
                    if (methodSymbol.ContainingType.TypeKind == TypeKind.Interface &&
                        (methodSymbol.MethodKind == MethodKind.PropertyGet || !HasSideEffects(methodSymbol)))
                    {
                        return true;
                    }

                    // Check if all arguments are pure
                    return invocation.ArgumentList?.Arguments.All(arg =>
                        IsExpressionPure(arg.Expression, semanticModel, currentMethod)) ?? true;

                case MemberAccessExpressionSyntax memberAccess:
                    // Check if this is a member of an impure type like Console
                    if (memberAccess.Expression is IdentifierNameSyntax identifierName)
                    {
                        if (identifierName.Identifier.ValueText == "Console")
                        {
                            // Console methods are impure
                            if (memberAccess.Name.Identifier.ValueText is "WriteLine" or "Write" or "ReadLine" or "Read")
                                return false;
                        }
                    }

                    var memberSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (memberSymbol is IFieldSymbol fieldSymbol)
                    {
                        // Static fields that are not constants are impure
                        if (fieldSymbol.IsStatic && !fieldSymbol.IsConst)
                            return false;

                        // Mutable instance fields should be checked for immutability
                        if (!fieldSymbol.IsReadOnly && !fieldSymbol.IsConst)
                        {
                            // If we're in a parent method, we need to check if the field is accessed in a mutable way
                            if (currentMethod != null && fieldSymbol.ContainingType.Equals(currentMethod.ContainingType, SymbolEqualityComparer.Default))
                            {
                                // If we're just reading the field (the member access is not on the left side of an assignment),
                                // then this is a pure operation
                                var isLeftSideOfAssignment = memberAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == memberAccess;

                                if (!isLeftSideOfAssignment)
                                    return true;

                                // Otherwise, this is an impure operation
                                return false;
                            }
                        }
                    }

                    // Property access
                    if (memberSymbol is IPropertySymbol propertySymbol)
                    {
                        // Check if property is an auto-implemented property and its backing field is readonly
                        if (propertySymbol.IsReadOnly)
                            return true;

                        // For normal properties, check if it has a setter and is on the left side of an assignment
                        if (propertySymbol.SetMethod != null)
                        {
                            var isLeftSideOfAssignment = memberAccess.Parent is AssignmentExpressionSyntax assignment && assignment.Left == memberAccess;
                            if (isLeftSideOfAssignment)
                                return false;
                        }
                    }

                    return true;

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
                    if (semanticModel.GetSymbolInfo(objectCreation.Type).Symbol is not ITypeSymbol typeSymbol)
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
                    // Increment and decrement operators modify state, so they are impure
                    if (prefix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                        prefix.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                        return false;
                    return IsExpressionPure(prefix.Operand, semanticModel, currentMethod);

                case PostfixUnaryExpressionSyntax postfix:
                    // Increment and decrement operators modify state, so they are impure
                    if (postfix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ||
                        postfix.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
                        return false;
                    return IsExpressionPure(postfix.Operand, semanticModel, currentMethod);

                case SimpleLambdaExpressionSyntax lambda:
                    var lambdaDataFlow = semanticModel.AnalyzeDataFlow(lambda.Body);
                    if (!lambdaDataFlow.Succeeded || DataFlowChecker.HasImpureCaptures(lambdaDataFlow))
                        return false;

                    // Check for field access within lambda that modify state
                    if (lambda.Body is BlockSyntax lambdaBlock)
                    {
                        // Check each statement in the lambda for field modification
                        foreach (var stmt in lambdaBlock.Statements)
                        {
                            // Check for field modification in expressions
                            var assignments = stmt.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>();
                            foreach (var assignment in assignments)
                            {
                                if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                                {
                                    // Check if we're accessing a field - any field modification in a lambda is suspicious
                                    var lambdaFieldSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                                    if (lambdaFieldSymbol is IFieldSymbol)
                                    {
                                        return false;
                                    }
                                }
                            }

                            // Check for Add/AddRange calls on collections
                            var invocations = stmt.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
                            foreach (var invocation in invocations)
                            {
                                if (invocation.Expression is MemberAccessExpressionSyntax memberAccessInvoke)
                                {
                                    if (memberAccessInvoke.Name.Identifier.Text is "Add" or "AddRange" or "Insert" or "Push" or "Enqueue")
                                    {
                                        // Check if the collection being modified is a field
                                        var collectionSymbol = semanticModel.GetSymbolInfo(memberAccessInvoke.Expression).Symbol;
                                        if (collectionSymbol is IFieldSymbol)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (lambda.Body is ExpressionSyntax lambdaExpr)
                    {
                        // For expression bodies, check if it's a member access to a field
                        if (lambdaExpr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                 ma.Name.Identifier.Text is "Add" or "AddRange" or "Insert" or "Push" or "Enqueue" &&
                                 semanticModel.GetSymbolInfo(ma.Expression).Symbol is IFieldSymbol))
                        {
                            return false;
                        }
                    }

                    return lambda.Body is ExpressionSyntax bodyExpr ? IsExpressionPure(bodyExpr, semanticModel, currentMethod) : false;

                case ParenthesizedLambdaExpressionSyntax lambda:
                    var lambdaBlockDataFlow = semanticModel.AnalyzeDataFlow(lambda.Body);
                    if (!lambdaBlockDataFlow.Succeeded || DataFlowChecker.HasImpureCaptures(lambdaBlockDataFlow))
                        return false;

                    // Check for field access within lambda that modify state
                    if (lambda.Body is BlockSyntax parenLambdaBlock)
                    {
                        // Check each statement in the lambda for field modification
                        foreach (var stmt in parenLambdaBlock.Statements)
                        {
                            // Check for field modification in expressions
                            var assignments = stmt.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>();
                            foreach (var assignment in assignments)
                            {
                                if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                                {
                                    // Check if we're accessing a field - any field modification in a lambda is suspicious
                                    var parenLambdaFieldSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                                    if (parenLambdaFieldSymbol is IFieldSymbol)
                                    {
                                        return false;
                                    }
                                }
                            }

                            // Check for Add/AddRange calls on collections
                            var invocations = stmt.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
                            foreach (var invocation in invocations)
                            {
                                if (invocation.Expression is MemberAccessExpressionSyntax memberAccessInvoke)
                                {
                                    if (memberAccessInvoke.Name.Identifier.Text is "Add" or "AddRange" or "Insert" or "Push" or "Enqueue")
                                    {
                                        // Check if the collection being modified is a field
                                        var collectionSymbol = semanticModel.GetSymbolInfo(memberAccessInvoke.Expression).Symbol;
                                        if (collectionSymbol is IFieldSymbol)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (lambda.Body is ExpressionSyntax parenLambdaExpr)
                    {
                        // For expression bodies, check if it's a member access to a field
                        if (parenLambdaExpr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                                 ma.Name.Identifier.Text is "Add" or "AddRange" or "Insert" or "Push" or "Enqueue" &&
                                 semanticModel.GetSymbolInfo(ma.Expression).Symbol is IFieldSymbol))
                        {
                            return false;
                        }
                    }

                    return lambda.Body switch
                    {
                        ExpressionSyntax exprBody => IsExpressionPure(exprBody, semanticModel, currentMethod),
                        BlockSyntax block => StatementPurityChecker.AreStatementsPure(block.Statements, semanticModel, currentMethod),
                        _ => false
                    };

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

                    // Handle field assignment - this will catch "_count++" cases
                    if (assignment.Left is IdentifierNameSyntax fieldIdentifier)
                    {
                        var fieldIdentifierSymbol = semanticModel.GetSymbolInfo(fieldIdentifier).Symbol;
                        if (fieldIdentifierSymbol is IFieldSymbol)
                        {
                            // Field assignments are impure
                            return false;
                        }
                    }

                    // Handle property assignment
                    if (assignment.Left is MemberAccessExpressionSyntax propertyAccess)
                    {
                        var accessedSymbol = semanticModel.GetSymbolInfo(propertyAccess).Symbol;

                        // If it's a field, assignments are always impure
                        if (accessedSymbol is IFieldSymbol)
                        {
                            return false;
                        }

                        if (accessedSymbol is IPropertySymbol recordPropertySymbol)
                        {
                            // Check if it's a record property
                            var containingType = recordPropertySymbol.ContainingType;
                            if (containingType != null && SymbolPurityChecker.IsPureSymbol(recordPropertySymbol))
                            {
                                return IsExpressionPure(propertyAccess.Expression, semanticModel, currentMethod) &&
                                       IsExpressionPure(assignment.Right, semanticModel, currentMethod);
                            }
                        }
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

                case AwaitExpressionSyntax awaitExpression:
                    // Check the awaited expression
                    var awaitedExpression = awaitExpression.Expression;

                    // Special case for Task.CompletedTask or Task.FromResult - these are pure
                    if (awaitedExpression is MemberAccessExpressionSyntax ma &&
                        ma.Expression is IdentifierNameSyntax ins &&
                        ins.Identifier.ValueText == "Task" &&
                        ma.Name.Identifier.ValueText == "CompletedTask")
                    {
                        return true;
                    }

                    if (awaitedExpression is InvocationExpressionSyntax invoc)
                    {
                        // Task.FromResult is pure
                        if (invoc.Expression is MemberAccessExpressionSyntax maInvoc &&
                            maInvoc.Expression is IdentifierNameSyntax insInvoc &&
                            insInvoc.Identifier.ValueText == "Task" &&
                            maInvoc.Name.Identifier.ValueText == "FromResult")
                        {
                            return true;
                        }
                    }

                    // For other awaits, check if the expression is pure
                    return IsExpressionPure(awaitedExpression, semanticModel, currentMethod);

                default:
                    return false;
            }
        }

        // Helper methods for checking purity
        private static bool IsKnownPureMethod(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check if the method is from known pure namespaces
            var containingNamespace = methodSymbol.ContainingNamespace?.ToString() ?? string.Empty;

            if (containingNamespace.StartsWith("System.Linq") ||
                containingNamespace.StartsWith("System.Collections.Immutable"))
                return true;

            // Check for specific pure types
            var containingType = methodSymbol.ContainingType?.ToString() ?? string.Empty;
            if (containingType == "System.Math" ||
                containingType == "System.String" ||
                containingType == "System.Int32" ||
                containingType == "System.Double")
                return true;

            return false;
        }

        private static bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check for EnforcePure attribute
            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure" ||
                attr.AttributeClass?.ToDisplayString().Contains("EnforcePure") == true);
        }

        private static bool HasSideEffects(IMethodSymbol methodSymbol)
        {
            // Methods that commonly have no side effects
            if (methodSymbol.Name is "ToString" or "GetHashCode" or "Equals" or "CompareTo" or "GetEnumerator")
                return false;

            return true; // Assume methods have side effects by default
        }
    }
}