using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp
{
    public static class ExpressionPurityChecker
    {
        public static bool IsExpressionPure(ExpressionSyntax expression, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            // If we're dealing with a null expression, consider it pure
            if (expression == null)
                return true;

            // Check the expression type
            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    // All literals including raw string literals are pure
                    return true;

                case TypeOfExpressionSyntax typeOfExpr:
                    // typeof expressions are evaluated at compile time and are pure
                    return true;

                case IdentifierNameSyntax identifier:
                    var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                    if (symbol != null)
                    {
                        if (symbol is IParameterSymbol parameter)
                        {
                            // Only consider Out and Ref parameters as impure
                            // In (readonly ref) parameters are considered pure
                            if (parameter.RefKind == RefKind.Out || parameter.RefKind == RefKind.Ref)
                                return false;
                            return true;
                        }
                        if (symbol is IFieldSymbol fieldSymbolIdentifier)
                        {
                            // Static fields that are not const are impure
                            if (fieldSymbolIdentifier.IsStatic && !fieldSymbolIdentifier.IsConst)
                                return false;

                            // Volatile fields are impure (both reading and writing)
                            if (fieldSymbolIdentifier.IsVolatile)
                            {
                                // Add specific handling for volatile field access
                                return false;
                            }
                        }
                    }
                    return symbol != null && SymbolPurityChecker.IsPureSymbol(symbol);

                case InvocationExpressionSyntax invocation:
                    // Check if it's a nameof expression, which is pure
                    var constantValue = semanticModel.GetConstantValue(invocation);
                    if (constantValue.HasValue)
                    {
                        return true;
                    }

                    var methodSymbol = semanticModel.GetSymbolInfo(invocation.Expression).Symbol as IMethodSymbol;
                    if (methodSymbol == null)
                    {
                        // If we can't determine the method symbol, we need to check if it's a dynamic invocation
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                        {
                            var expressionType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
                            if (expressionType != null && expressionType.TypeKind == TypeKind.Dynamic)
                            {
                                // Dynamic invocations are impure
                                return false;
                            }
                        }

                        // If we still can't determine the method, assume it's impure
                        return false;
                    }

                    // Special case for System.Enum.TryParse methods - consider them pure despite having out parameters
                    if (methodSymbol.Name == "TryParse" &&
                        methodSymbol.ContainingType?.Name == "Enum" &&
                        methodSymbol.ContainingType.ContainingNamespace?.Name == "System")
                    {
                        // Consider Enum.TryParse as pure even though it has out parameters
                        return invocation.ArgumentList?.Arguments.All(arg =>
                            arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) ||
                            IsExpressionPure(arg.Expression, semanticModel, currentMethod)) ?? true;
                    }

                    // Check if the method is known to be impure
                    if (MethodPurityChecker.IsKnownImpureMethod(methodSymbol))
                        return false;

                    // Check if method is in an impure namespace
                    if (methodSymbol.ContainingType != null && NamespaceChecker.IsInImpureNamespace(methodSymbol.ContainingType))
                        return false;

                    // Check if the method is known to be pure
                    if (MethodPurityChecker.IsKnownPureMethod(methodSymbol))
                        return true;

                    // Interface method that are property getters or have no side effects
                    if (methodSymbol.ContainingType != null &&
                        methodSymbol.ContainingType.TypeKind == TypeKind.Interface &&
                        (methodSymbol.MethodKind == MethodKind.PropertyGet || !HasSideEffects(methodSymbol)))
                    {
                        return true;
                    }

                    // Check if all arguments are pure
                    return invocation.ArgumentList?.Arguments.All(arg =>
                        IsExpressionPure(arg.Expression, semanticModel, currentMethod)) ?? true;

                case MemberAccessExpressionSyntax memberAccess:
                    // Check if it's an enum member access (which is pure)
                    var memberSymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
                    if (memberSymbol != null &&
                        memberSymbol.Kind == SymbolKind.Field &&
                        memberSymbol.ContainingType?.TypeKind == TypeKind.Enum)
                    {
                        // Enum member access is always pure
                        return true;
                    }

                    // For member access without assignment, reading dynamic properties is considered pure
                    // Only parent nodes that are invocations or assignments make dynamic operations impure
                    // This allows reading properties of dynamic objects

                    // Check if this is a member of an impure type like Console
                    if (memberAccess.Expression is IdentifierNameSyntax identifierName)
                    {
                        if (identifierName.Identifier.ValueText == "Console")
                        {
                            // Console methods are impure
                            if (memberAccess.Name.Identifier.ValueText is "WriteLine" or "Write" or "ReadLine" or "Read")
                                return false;
                        }

                        // Check access to Enum static methods (these are pure)
                        var type = semanticModel.GetSymbolInfo(identifierName).Symbol as ITypeSymbol;
                        if (type?.Name == "Enum" && type.ContainingNamespace?.Name == "System")
                        {
                            // Enum methods like Parse, TryParse, etc. are pure
                            return true;
                        }
                    }

                    if (memberSymbol is IFieldSymbol fieldSymbol)
                    {
                        // Static fields that are not const are impure
                        if (fieldSymbol.IsStatic && !fieldSymbol.IsConst)
                            return false;

                        // Volatile fields are impure (both reading and writing)
                        if (fieldSymbol.IsVolatile)
                        {
                            // This is impure regardless of whether it's read or written
                            return false;
                        }

                        // Mutable instance fields should be checked for immutability
                        if (!fieldSymbol.IsReadOnly && !fieldSymbol.IsConst)
                        {
                            // If we're in a parent method, we need to check if the field is accessed in a mutable way
                            if (currentMethod != null && SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, currentMethod.ContainingType))
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

                    // If we can't determine the symbol, be conservative and consider it impure
                    return false;

                // Add support for collection expressions (C# 12)
                case CollectionExpressionSyntax collectionExpr:
                    // Check the target type of the collection expression
                    var typeInfo = semanticModel.GetTypeInfo(collectionExpr);
                    var destinationType = typeInfo.ConvertedType;

                    // If we can't determine the type, be conservative and mark it as impure
                    if (destinationType == null)
                        return false;

                    // Check if the target type is a mutable collection
                    if (CollectionChecker.IsModifiableCollectionType(destinationType))
                        return false;

                    // Check all elements in the collection expression
                    foreach (var element in collectionExpr.Elements)
                    {
                        if (element is ExpressionElementSyntax exprElement)
                        {
                            if (!IsExpressionPure(exprElement.Expression, semanticModel, currentMethod))
                                return false;
                        }
                        else if (element is SpreadElementSyntax spreadElement)
                        {
                            if (!IsExpressionPure(spreadElement.Expression, semanticModel, currentMethod))
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
                    // Check if the constructor is pure
                    var constructorSymbol = semanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
                    if (constructorSymbol != null)
                    {
                        // If the constructor is a pure method, then check if all the arguments are pure
                        if (MethodPurityChecker.IsKnownPureMethod(constructorSymbol))
                        {
                            return objectCreation.ArgumentList == null ||
                                   objectCreation.ArgumentList.Arguments.All(a => IsExpressionPure(a.Expression, semanticModel, currentMethod));
                        }
                        // If the constructor is not a pure method, then the expression is impure
                        return false;
                    }
                    // If we can't determine the constructor symbol, be conservative and say it's impure
                    return false;

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
                    // Special case for delegate combination
                    if (binary.IsKind(SyntaxKind.AddExpression))
                    {
                        var leftType = semanticModel.GetTypeInfo(binary.Left).Type;
                        if (leftType?.TypeKind == TypeKind.Delegate)
                        {
                            // Delegate combination (+) is pure if both operands are pure
                            return IsExpressionPure(binary.Left, semanticModel, currentMethod) &&
                                   IsExpressionPure(binary.Right, semanticModel, currentMethod);
                        }
                    }
                    // For other binary expressions 
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
                    // Conditional expressions are pure if all their parts are pure
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
                    // Lambda expressions are pure if their body is pure and they don't capture impure variables
                    if (lambda.Body is ExpressionSyntax lambdaExpression)
                    {
                        // Check if the lambda body is a pure expression
                        bool bodyIsPure = IsExpressionPure(lambdaExpression, semanticModel, currentMethod);
                        if (!bodyIsPure)
                            return false;
                    }
                    else if (lambda.Body is BlockSyntax lambdaBlock)
                    {
                        // Check if the lambda body contains only pure statements
                        bool bodyIsPure = StatementPurityChecker.AreStatementsPure(lambdaBlock.Statements, semanticModel, currentMethod);
                        if (!bodyIsPure)
                            return false;
                    }

                    // Check if the lambda captures any variables
                    var dataFlow = semanticModel.AnalyzeDataFlow(lambda);
                    if (dataFlow != null && dataFlow.Succeeded)
                    {
                        // Check if any captured variables are fields or properties that might be impure
                        foreach (var capturedVar in dataFlow.CapturedInside.Union(dataFlow.CapturedOutside))
                        {
                            if (capturedVar is IFieldSymbol capturedField)
                            {
                                // If the field is volatile or non-readonly, consider it impure
                                if (capturedField.IsVolatile || !capturedField.IsReadOnly)
                                    return false;
                            }
                            else if (capturedVar is IPropertySymbol capturedProperty)
                            {
                                // If the property has a setter, consider it impure
                                if (capturedProperty.SetMethod != null)
                                    return false;
                            }
                            // Use SymbolEqualityComparer for comparison (Fix for RS1024)
                            // Check if the captured variable is 'this' (the containing symbol of the current method)
                            // Only proceed with further checks if it's NOT 'this'
                            if (currentMethod != null && !SymbolEqualityComparer.Default.Equals(capturedVar, currentMethod.ContainingSymbol))
                            {
                                // If it's not 'this', check if it's a local or parameter (implicitly pure if passed checks above)
                                if (!(capturedVar is ILocalSymbol) && !(capturedVar is IParameterSymbol))
                                {
                                    // Potentially a captured variable from an outer scope that needs deeper analysis later
                                    // For now, conservatively assume impure if not local/parameter/this.
                                    // return false; // Consider uncommenting for stricter initial check
                                }
                            }
                        }
                    }

                    // If we've reached here, the lambda is pure
                    return true;

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

                case ElementAccessExpressionSyntax arrayElementAccess:
                    // Get the type of the expression being accessed
                    var elementAccessedType = semanticModel.GetTypeInfo(arrayElementAccess.Expression).Type;

                    // First, check if this is an array or collection access
                    bool isElementAssignment = arrayElementAccess.Parent is AssignmentExpressionSyntax assignmentExpr && assignmentExpr.Left == arrayElementAccess;

                    // Check if this is accessing an indexer property
                    var elementAccessSymbol = semanticModel.GetSymbolInfo(arrayElementAccess).Symbol;
                    if (elementAccessSymbol is IPropertySymbol indexerSymbol && indexerSymbol.IsIndexer)
                    {
                        // Writing to an indexer is always impure
                        if (isElementAssignment)
                            return false;

                        // Reading from an indexer is pure (regardless of whether it has a setter)
                        // Just check that the expression and arguments are pure
                        return IsExpressionPure(arrayElementAccess.Expression, semanticModel, currentMethod) &&
                               arrayElementAccess.ArgumentList.Arguments.All(arg =>
                                   IsExpressionPure(arg.Expression, semanticModel, currentMethod));
                    }

                    // If we're writing to the element (assignment), it's impure
                    if (isElementAssignment)
                        return false;

                    // Check if this is an inline array
                    if (elementAccessedType != null && IsInlineArrayType(elementAccessedType))
                    {
                        // Reading from an inline array is pure, writing is impure
                        // We already checked if it's an assignment above
                        return IsExpressionPure(arrayElementAccess.Expression, semanticModel, currentMethod) &&
                               arrayElementAccess.ArgumentList.Arguments.All(arg =>
                                   IsExpressionPure(arg.Expression, semanticModel, currentMethod));
                    }

                    // Regular array/collection access - check expression and arguments
                    return IsExpressionPure(arrayElementAccess.Expression, semanticModel, currentMethod) &&
                           arrayElementAccess.ArgumentList.Arguments.All(arg =>
                               IsExpressionPure(arg.Expression, semanticModel, currentMethod));

                case AssignmentExpressionSyntax assignment:
                    // Check for dynamic property assignment - modifying dynamic properties is impure
                    if (assignment.Left is MemberAccessExpressionSyntax assignmentDynamicAccess)
                    {
                        var assignmentTypeInfo = semanticModel.GetTypeInfo(assignmentDynamicAccess.Expression);
                        if (assignmentTypeInfo.Type != null &&
                            (assignmentTypeInfo.Type.TypeKind == TypeKind.Dynamic ||
                             assignmentTypeInfo.Type.Name == "Object" &&
                             IsDynamicExpression(assignmentDynamicAccess.Expression, semanticModel)))
                        {
                            // Assigning to dynamic property is impure
                            return false;
                        }
                    }

                    // Handle tuple deconstruction
                    if (assignment.Left is TupleExpressionSyntax tupleLeft)
                    {
                        return tupleLeft.Arguments.All(arg => IsExpressionPure(arg.Expression, semanticModel, currentMethod)) &&
                               IsExpressionPure(assignment.Right, semanticModel, currentMethod);
                    }
                    // Handle array/span element assignment
                    if (assignment.Left is ElementAccessExpressionSyntax elementAccess)
                    {
                        // Check if we're assigning to an array parameter - specifically handle params arrays
                        var arrayExpression = elementAccess.Expression;
                        var arraySymbol = semanticModel.GetSymbolInfo(arrayExpression).Symbol;

                        // Check if this is an indexer property assignment
                        var elementAssignSymbol = semanticModel.GetSymbolInfo(elementAccess).Symbol;
                        if (elementAssignSymbol is IPropertySymbol indexerPropSymbol && indexerPropSymbol.IsIndexer)
                        {
                            // Writing to an indexer property is impure
                            return false;
                        }

                        if (arraySymbol is IParameterSymbol paramSymbol)
                        {
                            // Check if we're modifying a params array parameter
                            if (paramSymbol.IsParams)
                            {
                                return false; // Modifying a params array parameter is impure
                            }
                        }

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
                        var propertyAccessSymbol = semanticModel.GetSymbolInfo(propertyAccess).Symbol;

                        // If it's a field, assignments are always impure
                        if (propertyAccessSymbol is IFieldSymbol)
                            return false;

                        if (propertyAccessSymbol is IPropertySymbol recordPropertySymbol)
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

                case WithExpressionSyntax withExpr:
                    var operation = semanticModel.GetOperation(withExpr);
                    // Ensure operation is not null and handle potential null currentMethod
                    if (operation is IWithOperation withOperation && currentMethod != null) 
                    {
                        // Check the purity of the operand (the object being copied)
                        bool operandIsPure = IsIOperationPure(withOperation.Operand, semanticModel, currentMethod);
                        if (!operandIsPure) return false;

                        // Check the purity of the initializer expressions (the values being assigned)
                        // Initializer is IObjectOrCollectionInitializerOperation which contains Initializers
                        bool initializersArePure = true;
                        if (withOperation.Initializer != null)
                        {
                            foreach (var initOperation in withOperation.Initializer.Initializers)
                            {
                                // Assuming initializers are ISimpleAssignmentOperation or similar with a Value property
                                if (initOperation is ISimpleAssignmentOperation assignmentOp) {
                                    if (!IsIOperationPure(assignmentOp.Value, semanticModel, currentMethod))
                                    {
                                        initializersArePure = false;
                                        break;
                                    }
                                }
                                else if (initOperation is IMemberInitializerOperation memberInitOp) { // Handle member initializers
                                     if (!IsIOperationPure(memberInitOp.Initializer, semanticModel, currentMethod)) {
                                        initializersArePure = false;
                                        break;
                                     }
                                }
                                else {
                                    // Unknown initializer type, assume impure
                                    initializersArePure = false; 
                                    break;
                                }
                            }
                        }
                        if (!initializersArePure) return false;
                        
                        // Additionally, check the compiler-generated method if available (e.g., Clone or constructor)
                        // Placeholder: Assume pure for now if operand and initializers are pure.

                        // If operand and initializers are pure
                        return true; 
                    }
                    // Fallback if GetOperation fails, is wrong type, or currentMethod is null
                    return false; 

                default:
                    // For other types of expressions that we don't recognize yet, be conservative and say they're impure
                    return false;
            }
        }

        public static bool IsDynamicExpression(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            if (expression is IdentifierNameSyntax identifier)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol is IParameterSymbol parameter)
                {
                    // Check if the parameter is declared as dynamic
                    return parameter.Type.TypeKind == TypeKind.Dynamic ||
                           parameter.Type.Name == "Object" && parameter.DeclaringSyntaxReferences
                               .Select(r => r.GetSyntax())
                               .OfType<ParameterSyntax>()
                               .Any(p => p.Type?.ToString() == "dynamic");
                }
                else if (symbolInfo.Symbol is ILocalSymbol local)
                {
                    // Check if the local variable is declared as dynamic
                    return local.Type.TypeKind == TypeKind.Dynamic ||
                           local.Type.Name == "Object" && local.DeclaringSyntaxReferences
                               .Select(r => r.GetSyntax())
                               .OfType<VariableDeclaratorSyntax>()
                               .Any(v => v.Parent is VariableDeclarationSyntax vd && vd.Type.ToString() == "dynamic");
                }
            }
            return false;
        }

        // Helper methods for checking purity
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

        // Helper method to check if a type is an inline array
        private static bool IsInlineArrayType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                return false;

            // Check if the type has the [InlineArray] attribute
            return typeSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InlineArrayAttribute" && attr.AttributeClass.ContainingNamespace.ToDisplayString() == "System.Runtime.CompilerServices");
        }

        // Helper to check delegate purity based on the symbol holding the delegate
        public static bool IsDelegateSymbolPure(ISymbol delegateHolderSymbol, ExpressionSyntax expressionSyntax, SemanticModel semanticModel, HashSet<IMethodSymbol> visitedMethods)
        {
            // Check if the holder itself is immutable (e.g., readonly field)
            if (delegateHolderSymbol is IFieldSymbol fieldSymbol && !fieldSymbol.IsReadOnly && !fieldSymbol.IsConst)
            {
                // If a mutable field holds the delegate, assume impure
                return false;
            }
            if (delegateHolderSymbol is IPropertySymbol propertySymbol && propertySymbol.SetMethod != null)
            {
                // If a property with a setter holds the delegate, assume impure
                return false;
            }

            // Try to determine the target of the delegate if possible
            // This is complex; for now, let's assume purity if the holder is immutable
            // A more sophisticated analysis would trace the delegate's assignment

            // Example: If the delegate is assigned a lambda or method group, check its purity
            var dataFlow = semanticModel.AnalyzeDataFlow(expressionSyntax);
            if (dataFlow != null && dataFlow.Succeeded && dataFlow.DataFlowsIn.Length > 0)
            {
                // Simplified: If we can track a single flow-in, analyze it
                // This part needs significant expansion for real-world scenarios
            }

            // Conservative default: If holder is immutable, assume delegate invocation might be pure
            // We rely on the analysis of the invoked method symbol (if available)
            // Or, if the holder is a parameter, assume purity (caller's responsibility)
            return delegateHolderSymbol is IParameterSymbol || 
                   (delegateHolderSymbol is IFieldSymbol fs && (fs.IsReadOnly || fs.IsConst)) ||
                   (delegateHolderSymbol is IPropertySymbol ps && ps.SetMethod == null);
        }

        // Placeholder for a potential new helper method
        private static bool IsIOperationPure(IOperation? operation, SemanticModel semanticModel, IMethodSymbol? currentMethod)
        {
            // Handle null operation and null currentMethod
            if (operation == null) return true; // Null operation can be considered pure
            if (currentMethod == null) return false; // Cannot determine purity without method context

            // Example: Check based on operation Kind or recursively call IsExpressionPure on Syntax
            if (operation.Syntax is ExpressionSyntax exprSyntax) {
                return IsExpressionPure(exprSyntax, semanticModel, currentMethod);
            }
            
            // Add more checks based on different IOperation types as needed
            // (e.g., IInvocationOperation, IMemberReferenceOperation, ISimpleAssignmentOperation etc.)
            if (operation is ISimpleAssignmentOperation assignmentOp) {
                 // Check the right side (Value) for purity
                 return IsIOperationPure(assignmentOp.Value, semanticModel, currentMethod);
            }

            return false; // Default to impure if not explicitly handled
        }
    }
}