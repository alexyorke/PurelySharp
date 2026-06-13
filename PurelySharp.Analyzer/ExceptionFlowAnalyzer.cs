using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer
{
    internal static class ExceptionFlowAnalyzer
    {
        private const string UnknownExceptionType = "unknown";

        private static readonly SymbolDisplayFormat ExceptionTypeDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public static void AnalyzeSymbolForExceptions(
            SyntaxNodeAnalysisContext context,
            bool reportExceptions,
            ExceptionSummaryCatalog exceptionSummaryCatalog)
        {
            if (!Analyzer.Configuration.AnalyzerConfiguration.GetReportExceptions(
                    context.Options,
                    context.Node.SyntaxTree,
                    reportExceptions))
            {
                return;
            }

            if (!(context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is IMethodSymbol methodSymbol))
            {
                return;
            }

            if (methodSymbol.Locations.FirstOrDefault()?.IsInMetadata == true)
            {
                return;
            }

            var exceptionEvidence = CollectUncaughtExceptions(
                context.Node,
                context.SemanticModel,
                context.CancellationToken,
                methodSymbol,
                exceptionSummaryCatalog,
                new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
            if (exceptionEvidence.Count == 0)
            {
                return;
            }

            var diagnosticLocation = GetIdentifierLocation(context.Node);
            if (diagnosticLocation == null)
            {
                return;
            }

            var sortedTypes = exceptionEvidence.Types;
            var exceptionList = string.Join(", ", sortedTypes);
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(PurelySharpDiagnostics.ExceptionTypesProperty, string.Join(";", sortedTypes))
                .Add(PurelySharpDiagnostics.ExceptionCategoriesProperty, exceptionEvidence.FormatCategories())
                .Add(PurelySharpDiagnostics.ExceptionSourcesProperty, exceptionEvidence.FormatSources());

            context.ReportDiagnostic(Diagnostic.Create(
                PurelySharpDiagnostics.ExceptionSummaryRule,
                diagnosticLocation,
                additionalLocations: null,
                properties: properties,
                messageArgs: new object[] { methodSymbol.Name, exceptionList }));
        }

        private static ExceptionEvidenceSet CollectUncaughtExceptions(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            IMethodSymbol methodSymbol,
            ExceptionSummaryCatalog exceptionSummaryCatalog,
            HashSet<IMethodSymbol> visitedMethods)
        {
            visitedMethods.Add(methodSymbol.OriginalDefinition);
            var exceptionEvidence = new ExceptionEvidenceSet();
            foreach (var throwNode in GetThrowNodes(methodNode))
            {
                var exceptionType = GetThrownExceptionType(throwNode, semanticModel, cancellationToken);
                if (IsCaughtWithinMethod(throwNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                exceptionEvidence.Add(
                    exceptionType?.ToDisplayString(ExceptionTypeDisplayFormat) ?? UnknownExceptionType,
                    IsRethrow(throwNode) ? "rethrow" : "direct_throw",
                    "throw");
            }

            foreach (var invocation in GetInvocationNodes(methodNode))
            {
                if (!(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol invokedMethod))
                {
                    continue;
                }

                AddUncaughtCalleeExceptions(
                    exceptionEvidence,
                    invocation,
                    invokedMethod,
                    methodNode,
                    semanticModel,
                    cancellationToken,
                    exceptionSummaryCatalog,
                    visitedMethods);
            }

            foreach (var creation in GetObjectCreationNodes(methodNode))
            {
                if (!(semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol constructorSymbol))
                {
                    continue;
                }

                AddUncaughtCalleeExceptions(
                    exceptionEvidence,
                    creation,
                    constructorSymbol,
                    methodNode,
                    semanticModel,
                    cancellationToken,
                    exceptionSummaryCatalog,
                    visitedMethods);
            }

            foreach (var propertyAccess in GetPropertyAccessNodes(methodNode, semanticModel, cancellationToken))
            {
                if (!(semanticModel.GetSymbolInfo(propertyAccess, cancellationToken).Symbol is IPropertySymbol propertySymbol) ||
                    propertySymbol.GetMethod == null)
                {
                    continue;
                }

                AddUncaughtCalleeExceptions(
                    exceptionEvidence,
                    propertyAccess,
                    propertySymbol.GetMethod,
                    methodNode,
                    semanticModel,
                    cancellationToken,
                    exceptionSummaryCatalog,
                    visitedMethods);
            }

            foreach (var divideByZeroNode in GetDefiniteDivideByZeroNodes(methodNode, semanticModel, cancellationToken))
            {
                var exceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.DivideByZeroException");
                if (IsCaughtWithinMethod(divideByZeroNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                exceptionEvidence.Add("System.DivideByZeroException", "definite_divide_by_zero", "binary_operator");
            }

            foreach (var nullDereferenceNode in GetDefiniteNullDereferenceNodes(methodNode, semanticModel, cancellationToken))
            {
                var exceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.NullReferenceException");
                if (IsCaughtWithinMethod(nullDereferenceNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                exceptionEvidence.Add("System.NullReferenceException", "definite_null_dereference", "null_receiver");
            }

            return exceptionEvidence;
        }

        private static void AddUncaughtCalleeExceptions(
            ExceptionEvidenceSet exceptionEvidence,
            SyntaxNode callSite,
            IMethodSymbol callee,
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            ExceptionSummaryCatalog exceptionSummaryCatalog,
            HashSet<IMethodSymbol> visitedMethods)
        {
            foreach (var exception in CollectCalleeExceptions(
                         callee,
                         semanticModel.Compilation,
                         cancellationToken,
                         exceptionSummaryCatalog,
                         visitedMethods))
            {
                if (IsCaughtWithinMethod(callSite, exception.Type, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                exceptionEvidence.Add(exception.DisplayName, exception.Category, exception.Source);
            }
        }

        private static IEnumerable<SyntaxNode> GetThrowNodes(SyntaxNode methodNode)
        {
            return methodNode.DescendantNodes(
                    descendIntoChildren: node => ReferenceEquals(node, methodNode) || !IsNestedCallableBoundary(node))
                .Where(node => node is ThrowStatementSyntax || node is ThrowExpressionSyntax);
        }

        private static IEnumerable<InvocationExpressionSyntax> GetInvocationNodes(SyntaxNode methodNode)
        {
            return methodNode.DescendantNodes(
                    descendIntoChildren: node => ReferenceEquals(node, methodNode) || !IsNestedCallableBoundary(node))
                .OfType<InvocationExpressionSyntax>();
        }

        private static IEnumerable<SyntaxNode> GetObjectCreationNodes(SyntaxNode methodNode)
        {
            return methodNode.DescendantNodes(
                    descendIntoChildren: node => ReferenceEquals(node, methodNode) || !IsNestedCallableBoundary(node))
                .Where(node => node is ObjectCreationExpressionSyntax || node is ImplicitObjectCreationExpressionSyntax);
        }

        private static IEnumerable<BinaryExpressionSyntax> GetDefiniteDivideByZeroNodes(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var binaryExpression in methodNode.DescendantNodes(
                         descendIntoChildren: node => ReferenceEquals(node, methodNode) || !IsNestedCallableBoundary(node))
                         .OfType<BinaryExpressionSyntax>())
            {
                if (!binaryExpression.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.DivideExpression) &&
                    !binaryExpression.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ModuloExpression))
                {
                    continue;
                }

                var rightType = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken).ConvertedType;
                if (!IsThrowingDivideByZeroType(rightType))
                {
                    continue;
                }

                if (IsDefinitelyZeroExpression(binaryExpression.Right, binaryExpression, semanticModel, cancellationToken))
                {
                    yield return binaryExpression;
                }
            }
        }

        private static IEnumerable<SyntaxNode> GetDefiniteNullDereferenceNodes(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var node in methodNode.DescendantNodes(
                         descendIntoChildren: candidate => ReferenceEquals(candidate, methodNode) || !IsNestedCallableBoundary(candidate)))
            {
                if (node is MemberAccessExpressionSyntax memberAccess &&
                    IsDefinitelyNullExpression(memberAccess.Expression, memberAccess, semanticModel, cancellationToken))
                {
                    yield return memberAccess;
                }
                else if (node is ElementAccessExpressionSyntax elementAccess &&
                    IsDefinitelyNullExpression(elementAccess.Expression, elementAccess, semanticModel, cancellationToken))
                {
                    yield return elementAccess;
                }
                else if (node is InvocationExpressionSyntax invocation &&
                    IsDefinitelyNullExpression(invocation.Expression, invocation, semanticModel, cancellationToken))
                {
                    yield return invocation;
                }
            }
        }

        private static IEnumerable<SyntaxNode> GetPropertyAccessNodes(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var node in methodNode.DescendantNodes(
                         descendIntoChildren: candidate => ReferenceEquals(candidate, methodNode) || !IsNestedCallableBoundary(candidate)))
            {
                if (node is MemberAccessExpressionSyntax memberAccess)
                {
                    if (IsWriteOnlyTarget(memberAccess))
                    {
                        continue;
                    }

                    if (semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is IPropertySymbol)
                    {
                        yield return memberAccess;
                    }
                }
                else if (node is IdentifierNameSyntax identifierName)
                {
                    if (identifierName.Parent is MemberAccessExpressionSyntax ||
                        IsWriteOnlyTarget(identifierName))
                    {
                        continue;
                    }

                    if (semanticModel.GetSymbolInfo(identifierName, cancellationToken).Symbol is IPropertySymbol)
                    {
                        yield return identifierName;
                    }
                }
                else if (node is ElementAccessExpressionSyntax elementAccess)
                {
                    if (IsWriteOnlyTarget(elementAccess))
                    {
                        continue;
                    }

                    if (semanticModel.GetSymbolInfo(elementAccess, cancellationToken).Symbol is IPropertySymbol)
                    {
                        yield return elementAccess;
                    }
                }
            }
        }

        private static bool IsWriteOnlyTarget(SyntaxNode node)
        {
            return node.Parent is AssignmentExpressionSyntax assignment &&
                ReferenceEquals(assignment.Left, node);
        }

        private static bool IsNestedCallableBoundary(SyntaxNode node)
        {
            return node is MethodDeclarationSyntax ||
                node is ConstructorDeclarationSyntax ||
                node is OperatorDeclarationSyntax ||
                node is AccessorDeclarationSyntax ||
                node is LocalFunctionStatementSyntax ||
                node is ParenthesizedLambdaExpressionSyntax ||
                node is SimpleLambdaExpressionSyntax ||
                node is AnonymousMethodExpressionSyntax;
        }

        private static ITypeSymbol? GetThrownExceptionType(
            SyntaxNode throwNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            ExpressionSyntax? exceptionExpression = throwNode switch
            {
                ThrowStatementSyntax statement => statement.Expression,
                ThrowExpressionSyntax expression => expression.Expression,
                _ => null
            };

            if (exceptionExpression == null)
            {
                return throwNode is ThrowStatementSyntax statement
                    ? GetRethrownExceptionType(statement, semanticModel, cancellationToken)
                    : null;
            }

            var typeInfo = semanticModel.GetTypeInfo(exceptionExpression, cancellationToken);
            return typeInfo.Type ?? typeInfo.ConvertedType;
        }

        private static bool IsRethrow(SyntaxNode throwNode)
        {
            return throwNode is ThrowStatementSyntax statement && statement.Expression == null;
        }

        private static bool IsIntegralOrDecimalZero(object? value)
        {
            switch (value)
            {
                case byte byteValue:
                    return byteValue == 0;
                case sbyte sbyteValue:
                    return sbyteValue == 0;
                case short shortValue:
                    return shortValue == 0;
                case ushort ushortValue:
                    return ushortValue == 0;
                case int intValue:
                    return intValue == 0;
                case uint uintValue:
                    return uintValue == 0;
                case long longValue:
                    return longValue == 0L;
                case ulong ulongValue:
                    return ulongValue == 0UL;
                case decimal decimalValue:
                    return decimalValue == 0m;
                default:
                    return false;
            }
        }

        private static bool IsThrowingDivideByZeroType(ITypeSymbol? typeSymbol)
        {
            if (typeSymbol == null)
            {
                return false;
            }

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDefinitelyZeroExpression(
            ExpressionSyntax expression,
            SyntaxNode useNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            var constantValue = semanticModel.GetConstantValue(expression, cancellationToken);
            return (constantValue.HasValue && IsIntegralOrDecimalZero(constantValue.Value)) ||
                IsKnownByDominatingIf(expression, useNode, semanticModel, cancellationToken, PathFactKind.Zero);
        }

        private static bool IsDefinitelyNullExpression(
            ExpressionSyntax expression,
            SyntaxNode useNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            while (true)
            {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    expression = parenthesized.Expression;
                    continue;
                }

                if (expression is CastExpressionSyntax castExpression)
                {
                    if (IsDefinitelyNullExpression(castExpression.Expression, useNode, semanticModel, cancellationToken))
                    {
                        var castType = semanticModel.GetTypeInfo(castExpression, cancellationToken).Type;
                        return IsReferenceType(castType);
                    }

                    return false;
                }

                if (expression is PostfixUnaryExpressionSyntax postfixUnary &&
                    postfixUnary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression))
                {
                    expression = postfixUnary.Operand;
                    continue;
                }

                break;
            }

            if (expression.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression))
            {
                return true;
            }

            if (expression is DefaultExpressionSyntax defaultExpression)
            {
                var defaultType = semanticModel.GetTypeInfo(defaultExpression, cancellationToken).Type;
                return IsReferenceType(defaultType);
            }

            return IsKnownByDominatingIf(expression, useNode, semanticModel, cancellationToken, PathFactKind.Null);
        }

        private static bool IsKnownByDominatingIf(
            ExpressionSyntax expression,
            SyntaxNode useNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            PathFactKind factKind)
        {
            var symbol = GetLocalOrParameterSymbol(expression, semanticModel, cancellationToken);
            if (symbol == null)
            {
                return false;
            }

            foreach (var ifStatement in useNode.Ancestors().OfType<IfStatementSyntax>())
            {
                if (ifStatement.Statement.Span.Contains(useNode.SpanStart) &&
                    ConditionImpliesFact(ifStatement.Condition, symbol, factKind, branchWhenTrue: true, semanticModel, cancellationToken) &&
                    !IsSymbolAssignedBeforeUse(ifStatement.Statement, useNode.SpanStart, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }

                if (ifStatement.Else?.Statement is { } elseStatement &&
                    elseStatement.Span.Contains(useNode.SpanStart) &&
                    ConditionImpliesFact(ifStatement.Condition, symbol, factKind, branchWhenTrue: false, semanticModel, cancellationToken) &&
                    !IsSymbolAssignedBeforeUse(elseStatement, useNode.SpanStart, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static ISymbol? GetLocalOrParameterSymbol(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            expression = UnwrapFactExpression(expression);
            var symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
            return symbol is ILocalSymbol or IParameterSymbol ? symbol.OriginalDefinition : null;
        }

        private static ExpressionSyntax UnwrapFactExpression(ExpressionSyntax expression)
        {
            while (true)
            {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    expression = parenthesized.Expression;
                    continue;
                }

                if (expression is PostfixUnaryExpressionSyntax postfixUnary &&
                    postfixUnary.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                {
                    expression = postfixUnary.Operand;
                    continue;
                }

                return expression;
            }
        }

        private static bool ConditionImpliesFact(
            ExpressionSyntax condition,
            ISymbol symbol,
            PathFactKind factKind,
            bool branchWhenTrue,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            condition = UnwrapFactExpression(condition);
            if (condition is BinaryExpressionSyntax binaryExpression)
            {
                var equalityImpliesFact = binaryExpression.IsKind(SyntaxKind.EqualsExpression) && branchWhenTrue;
                var inequalityImpliesFact = binaryExpression.IsKind(SyntaxKind.NotEqualsExpression) && !branchWhenTrue;
                if (!equalityImpliesFact && !inequalityImpliesFact)
                {
                    return false;
                }

                return IsSymbolComparedToFact(binaryExpression.Left, binaryExpression.Right, symbol, factKind, semanticModel, cancellationToken) ||
                    IsSymbolComparedToFact(binaryExpression.Right, binaryExpression.Left, symbol, factKind, semanticModel, cancellationToken);
            }

            if (condition is IsPatternExpressionSyntax isPatternExpression &&
                branchWhenTrue &&
                ExpressionMatchesSymbol(isPatternExpression.Expression, symbol, semanticModel, cancellationToken) &&
                PatternMatchesFact(isPatternExpression.Pattern, factKind, semanticModel, cancellationToken))
            {
                return true;
            }

            return false;
        }

        private static bool IsSymbolComparedToFact(
            ExpressionSyntax symbolExpression,
            ExpressionSyntax factExpression,
            ISymbol symbol,
            PathFactKind factKind,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            return ExpressionMatchesSymbol(symbolExpression, symbol, semanticModel, cancellationToken) &&
                ExpressionMatchesFact(factExpression, factKind, semanticModel, cancellationToken);
        }

        private static bool ExpressionMatchesSymbol(
            ExpressionSyntax expression,
            ISymbol symbol,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            var expressionSymbol = GetLocalOrParameterSymbol(expression, semanticModel, cancellationToken);
            return expressionSymbol != null && SymbolEqualityComparer.Default.Equals(expressionSymbol, symbol);
        }

        private static bool ExpressionMatchesFact(
            ExpressionSyntax expression,
            PathFactKind factKind,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            expression = UnwrapFactExpression(expression);
            if (factKind == PathFactKind.Null)
            {
                return expression.IsKind(SyntaxKind.NullLiteralExpression);
            }

            var constantValue = semanticModel.GetConstantValue(expression, cancellationToken);
            return constantValue.HasValue && IsIntegralOrDecimalZero(constantValue.Value);
        }

        private static bool PatternMatchesFact(
            PatternSyntax pattern,
            PathFactKind factKind,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            if (pattern is ConstantPatternSyntax constantPattern)
            {
                return ExpressionMatchesFact(constantPattern.Expression, factKind, semanticModel, cancellationToken);
            }

            return false;
        }

        private static bool IsSymbolAssignedBeforeUse(
            SyntaxNode branchRoot,
            int useSpanStart,
            ISymbol symbol,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var node in branchRoot.DescendantNodes(
                         descendIntoChildren: candidate => !IsNestedCallableBoundary(candidate)))
            {
                if (node.SpanStart >= useSpanStart)
                {
                    continue;
                }

                if (node is AssignmentExpressionSyntax assignment &&
                    ExpressionMatchesSymbol(assignment.Left, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }

                if (node is PrefixUnaryExpressionSyntax prefixUnary &&
                    (prefixUnary.IsKind(SyntaxKind.PreIncrementExpression) || prefixUnary.IsKind(SyntaxKind.PreDecrementExpression)) &&
                    ExpressionMatchesSymbol(prefixUnary.Operand, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }

                if (node is PostfixUnaryExpressionSyntax postfixUnary &&
                    (postfixUnary.IsKind(SyntaxKind.PostIncrementExpression) || postfixUnary.IsKind(SyntaxKind.PostDecrementExpression)) &&
                    ExpressionMatchesSymbol(postfixUnary.Operand, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }

                if (node is ArgumentSyntax argument &&
                    !argument.RefKindKeyword.IsKind(SyntaxKind.None) &&
                    ExpressionMatchesSymbol(argument.Expression, symbol, semanticModel, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsReferenceType(ITypeSymbol? typeSymbol)
        {
            return typeSymbol != null &&
                typeSymbol.TypeKind != TypeKind.TypeParameter &&
                typeSymbol.IsReferenceType;
        }

        private static ITypeSymbol? GetRethrownExceptionType(
            ThrowStatementSyntax throwStatement,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var catchClause in throwStatement.Ancestors().OfType<CatchClauseSyntax>())
            {
                if (!catchClause.Block.Span.Contains(throwStatement.SpanStart))
                {
                    continue;
                }

                if (catchClause.Declaration == null)
                {
                    return null;
                }

                return semanticModel.GetTypeInfo(catchClause.Declaration.Type, cancellationToken).Type;
            }

            return null;
        }

        private static IEnumerable<ExceptionCandidate> CollectSourceCalleeExceptions(
            IMethodSymbol invokedMethod,
            Compilation compilation,
            System.Threading.CancellationToken cancellationToken,
            ExceptionSummaryCatalog exceptionSummaryCatalog,
            HashSet<IMethodSymbol> visitedMethods)
        {
            var originalDefinition = invokedMethod.OriginalDefinition;
            if (!visitedMethods.Add(originalDefinition))
            {
                return Enumerable.Empty<ExceptionCandidate>();
            }

            try
            {
                var syntaxReference = invokedMethod.DeclaringSyntaxReferences.FirstOrDefault()
                    ?? originalDefinition.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference == null)
                {
                    return Enumerable.Empty<ExceptionCandidate>();
                }

                var syntax = syntaxReference.GetSyntax(cancellationToken);
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                var exceptions = CollectUncaughtExceptions(
                    syntax,
                    semanticModel,
                    cancellationToken,
                    invokedMethod,
                    exceptionSummaryCatalog,
                    visitedMethods);

                return exceptions.Types
                    .Select(name => new ExceptionCandidate(
                        TryResolveExceptionType(compilation, name),
                        name,
                        "source_callee",
                        invokedMethod.OriginalDefinition.ToDisplayString()))
                    .ToArray();
            }
            finally
            {
                visitedMethods.Remove(originalDefinition);
            }
        }

        private static IEnumerable<ExceptionCandidate> CollectCalleeExceptions(
            IMethodSymbol invokedMethod,
            Compilation compilation,
            System.Threading.CancellationToken cancellationToken,
            ExceptionSummaryCatalog exceptionSummaryCatalog,
            HashSet<IMethodSymbol> visitedMethods)
        {
            foreach (var exception in CollectSourceCalleeExceptions(invokedMethod, compilation, cancellationToken, exceptionSummaryCatalog, visitedMethods))
            {
                yield return exception;
            }

            if (!exceptionSummaryCatalog.TryGetExceptions(invokedMethod, out var summaryExceptions))
            {
                yield break;
            }

            foreach (var exceptionType in summaryExceptions)
            {
                yield return new ExceptionCandidate(
                    TryResolveExceptionType(compilation, exceptionType),
                    exceptionType,
                    "effect_summary",
                    invokedMethod.OriginalDefinition.ToDisplayString());
            }
        }

        private static ITypeSymbol? TryResolveExceptionType(Compilation compilation, string displayName)
        {
            return displayName == UnknownExceptionType
                ? null
                : compilation.GetTypeByMetadataName(displayName);
        }

        private static bool IsCaughtWithinMethod(
            SyntaxNode throwNode,
            ITypeSymbol? exceptionType,
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            foreach (var tryStatement in throwNode.Ancestors().OfType<TryStatementSyntax>())
            {
                if (!tryStatement.Span.Contains(throwNode.SpanStart))
                {
                    continue;
                }

                if (!tryStatement.Block.Span.Contains(throwNode.SpanStart))
                {
                    continue;
                }

                if (tryStatement.Catches.Any(catchClause => CatchesException(catchClause, exceptionType, semanticModel, cancellationToken)))
                {
                    return true;
                }

                if (ReferenceEquals(tryStatement, methodNode))
                {
                    break;
                }
            }

            return false;
        }

        private static bool CatchesException(
            CatchClauseSyntax catchClause,
            ITypeSymbol? exceptionType,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken)
        {
            if (catchClause.Filter != null)
            {
                return false;
            }

            if (catchClause.Declaration == null)
            {
                return true;
            }

            if (exceptionType == null)
            {
                return false;
            }

            var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type, cancellationToken).Type;
            return catchType != null && IsSameOrDerivedFrom(exceptionType, catchType);
        }

        private static bool IsSameOrDerivedFrom(ITypeSymbol exceptionType, ITypeSymbol catchType)
        {
            for (var current = exceptionType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, catchType))
                {
                    return true;
                }
            }

            return false;
        }

        private static Location? GetIdentifierLocation(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax method => method.Identifier.GetLocation(),
                ConstructorDeclarationSyntax constructor => constructor.Identifier.GetLocation(),
                OperatorDeclarationSyntax op => op.OperatorToken.GetLocation(),
                LocalFunctionStatementSyntax localFunction => localFunction.Identifier.GetLocation(),
                AccessorDeclarationSyntax accessor =>
                    accessor.Parent?.Parent switch
                    {
                        PropertyDeclarationSyntax property => property.Identifier.GetLocation(),
                        IndexerDeclarationSyntax indexer => indexer.ThisKeyword.GetLocation(),
                        _ => accessor.Keyword.GetLocation()
                    } ?? accessor.Keyword.GetLocation(),
                _ => node.GetLocation()
            };
        }

        private sealed class ExceptionCandidate
        {
            public ExceptionCandidate(ITypeSymbol? type, string displayName, string category, string source)
            {
                Type = type;
                DisplayName = displayName;
                Category = category;
                Source = source;
            }

            public ITypeSymbol? Type { get; }

            public string DisplayName { get; }

            public string Category { get; }

            public string Source { get; }
        }

        private sealed class ExceptionEvidenceSet
        {
            private readonly Dictionary<string, SortedSet<string>> _categoriesByType =
                new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

            private readonly Dictionary<string, SortedSet<string>> _sourcesByType =
                new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

            public int Count => _categoriesByType.Count;

            public string[] Types => _categoriesByType.Keys.OrderBy(type => type, StringComparer.Ordinal).ToArray();

            public void Add(string exceptionType, string category, string source)
            {
                if (!_categoriesByType.TryGetValue(exceptionType, out var categories))
                {
                    categories = new SortedSet<string>(StringComparer.Ordinal);
                    _categoriesByType.Add(exceptionType, categories);
                }

                categories.Add(category);

                if (!_sourcesByType.TryGetValue(exceptionType, out var sources))
                {
                    sources = new SortedSet<string>(StringComparer.Ordinal);
                    _sourcesByType.Add(exceptionType, sources);
                }

                sources.Add(category + ":" + source);
            }

            public string FormatCategories()
            {
                return string.Join(
                    ";",
                    _categoriesByType.Values
                        .SelectMany(categories => categories)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(category => category, StringComparer.Ordinal));
            }

            public string FormatSources()
            {
                return string.Join(
                    ";",
                    _sourcesByType
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .SelectMany(item => item.Value.Select(source => item.Key + "=" + source)));
            }
        }

        private enum PathFactKind
        {
            Zero,
            Null
        }
    }
}
