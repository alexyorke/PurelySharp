using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
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

            var thrownTypes = CollectUncaughtExceptions(
                context.Node,
                context.SemanticModel,
                context.CancellationToken,
                methodSymbol,
                exceptionSummaryCatalog,
                new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
            if (thrownTypes.Count == 0)
            {
                return;
            }

            var diagnosticLocation = GetIdentifierLocation(context.Node);
            if (diagnosticLocation == null)
            {
                return;
            }

            var sortedTypes = thrownTypes.OrderBy(type => type, StringComparer.Ordinal).ToArray();
            var exceptionList = string.Join(", ", sortedTypes);
            var properties = ImmutableDictionary<string, string?>.Empty.Add(
                PurelySharpDiagnostics.ExceptionTypesProperty,
                string.Join(";", sortedTypes));

            context.ReportDiagnostic(Diagnostic.Create(
                PurelySharpDiagnostics.ExceptionSummaryRule,
                diagnosticLocation,
                additionalLocations: null,
                properties: properties,
                messageArgs: new object[] { methodSymbol.Name, exceptionList }));
        }

        private static HashSet<string> CollectUncaughtExceptions(
            SyntaxNode methodNode,
            SemanticModel semanticModel,
            System.Threading.CancellationToken cancellationToken,
            IMethodSymbol methodSymbol,
            ExceptionSummaryCatalog exceptionSummaryCatalog,
            HashSet<IMethodSymbol> visitedMethods)
        {
            visitedMethods.Add(methodSymbol.OriginalDefinition);
            var thrownTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var throwNode in GetThrowNodes(methodNode))
            {
                var exceptionType = GetThrownExceptionType(throwNode, semanticModel, cancellationToken);
                if (IsCaughtWithinMethod(throwNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                thrownTypes.Add(exceptionType?.ToDisplayString(ExceptionTypeDisplayFormat) ?? UnknownExceptionType);
            }

            foreach (var invocation in GetInvocationNodes(methodNode))
            {
                if (!(semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol invokedMethod))
                {
                    continue;
                }

                foreach (var exception in CollectCalleeExceptions(invokedMethod, semanticModel.Compilation, cancellationToken, exceptionSummaryCatalog, visitedMethods))
                {
                    if (IsCaughtWithinMethod(invocation, exception.Type, methodNode, semanticModel, cancellationToken))
                    {
                        continue;
                    }

                    thrownTypes.Add(exception.DisplayName);
                }
            }

            foreach (var creation in GetObjectCreationNodes(methodNode))
            {
                if (!(semanticModel.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol constructorSymbol))
                {
                    continue;
                }

                foreach (var exception in CollectCalleeExceptions(constructorSymbol, semanticModel.Compilation, cancellationToken, exceptionSummaryCatalog, visitedMethods))
                {
                    if (IsCaughtWithinMethod(creation, exception.Type, methodNode, semanticModel, cancellationToken))
                    {
                        continue;
                    }

                    thrownTypes.Add(exception.DisplayName);
                }
            }

            foreach (var propertyAccess in GetPropertyAccessNodes(methodNode, semanticModel, cancellationToken))
            {
                if (!(semanticModel.GetSymbolInfo(propertyAccess, cancellationToken).Symbol is IPropertySymbol propertySymbol) ||
                    propertySymbol.GetMethod == null)
                {
                    continue;
                }

                foreach (var exception in CollectCalleeExceptions(propertySymbol.GetMethod, semanticModel.Compilation, cancellationToken, exceptionSummaryCatalog, visitedMethods))
                {
                    if (IsCaughtWithinMethod(propertyAccess, exception.Type, methodNode, semanticModel, cancellationToken))
                    {
                        continue;
                    }

                    thrownTypes.Add(exception.DisplayName);
                }
            }

            foreach (var divideByZeroNode in GetDefiniteDivideByZeroNodes(methodNode, semanticModel, cancellationToken))
            {
                var exceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.DivideByZeroException");
                if (IsCaughtWithinMethod(divideByZeroNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                thrownTypes.Add("System.DivideByZeroException");
            }

            foreach (var nullDereferenceNode in GetDefiniteNullDereferenceNodes(methodNode, semanticModel, cancellationToken))
            {
                var exceptionType = semanticModel.Compilation.GetTypeByMetadataName("System.NullReferenceException");
                if (IsCaughtWithinMethod(nullDereferenceNode, exceptionType, methodNode, semanticModel, cancellationToken))
                {
                    continue;
                }

                thrownTypes.Add("System.NullReferenceException");
            }

            return thrownTypes;
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

                var rightConstant = semanticModel.GetConstantValue(binaryExpression.Right, cancellationToken);
                if (!rightConstant.HasValue || !IsIntegralOrDecimalZero(rightConstant.Value))
                {
                    continue;
                }

                var rightType = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken).ConvertedType;
                if (!IsThrowingDivideByZeroType(rightType))
                {
                    continue;
                }

                yield return binaryExpression;
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
                    IsDefinitelyNullExpression(memberAccess.Expression, semanticModel, cancellationToken))
                {
                    yield return memberAccess;
                }
                else if (node is ElementAccessExpressionSyntax elementAccess &&
                    IsDefinitelyNullExpression(elementAccess.Expression, semanticModel, cancellationToken))
                {
                    yield return elementAccess;
                }
                else if (node is InvocationExpressionSyntax invocation &&
                    IsDefinitelyNullExpression(invocation.Expression, semanticModel, cancellationToken))
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

        private static bool IsDefinitelyNullExpression(
            ExpressionSyntax expression,
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
                    if (IsDefinitelyNullExpression(castExpression.Expression, semanticModel, cancellationToken))
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

                return exceptions
                    .Select(name => new ExceptionCandidate(TryResolveExceptionType(compilation, name), name))
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
                yield return new ExceptionCandidate(TryResolveExceptionType(compilation, exceptionType), exceptionType);
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
            public ExceptionCandidate(ITypeSymbol? type, string displayName)
            {
                Type = type;
                DisplayName = displayName;
            }

            public ITypeSymbol? Type { get; }

            public string DisplayName { get; }
        }
    }
}
