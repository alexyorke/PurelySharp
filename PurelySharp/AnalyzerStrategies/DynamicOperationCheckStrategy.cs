using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.AnalyzerStrategies
{
    /// <summary>
    /// Strategy to check for the presence of dynamic operations within a syntax node, which are considered impure.
    /// </summary>
    public class DynamicOperationCheckStrategy : IPurityAnalyzerCheck
    {
        public CheckPurityResult Check(CSharpSyntaxNode node, SyntaxNodeAnalysisContext context)
        {
            var dynamicCheckResult = CheckForDynamicOperations(node, context.SemanticModel);
            if (dynamicCheckResult.Item1) // Impurity found
            {
                var location = dynamicCheckResult.Item2 ?? node.GetLocation();
                return CheckPurityResult.Fail(location, "Node contains dynamic operations");
            }
            return CheckPurityResult.Pass;
        }

        // --- Logic moved from PurelySharpAnalyzer --- 

        private (bool, Location?) CheckForDynamicOperations(CSharpSyntaxNode node, SemanticModel semanticModel)
        {
            foreach (var descendant in node.DescendantNodesAndSelf())
            {
                // Check for dynamic type usage in declarations
                if (descendant is VariableDeclarationSyntax varDecl)
                {
                    if (IsDynamicType(varDecl.Type, varDecl.Type, semanticModel))
                        return (true, varDecl.Type.GetLocation());
                }
                else if (descendant is ParameterSyntax paramSyntax)
                {
                    if (paramSyntax.Type != null && IsDynamicType(paramSyntax.Type, paramSyntax.Type, semanticModel))
                        return (true, paramSyntax.Type.GetLocation());
                }
                else if (descendant is MethodDeclarationSyntax methodDecl)
                {
                    if (IsDynamicType(methodDecl.ReturnType, methodDecl.ReturnType, semanticModel))
                        return (true, methodDecl.ReturnType.GetLocation());
                }

                // Check for dynamic invocations
                if (descendant is InvocationExpressionSyntax invocation)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                    {
                        // Check if return type is dynamic
                        if (methodSymbol.ReturnType.TypeKind == TypeKind.Dynamic)
                            return (true, invocation.GetLocation());

                        // Check if any parameter type is dynamic
                        if (methodSymbol.Parameters.Any(p => p.Type.TypeKind == TypeKind.Dynamic))
                        {
                            // Check if corresponding argument is dynamic
                            for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                            {
                                if (methodSymbol.Parameters[i].Type.TypeKind == TypeKind.Dynamic &&
                                    invocation.ArgumentList.Arguments.Count > i)
                                {
                                    var argExpr = invocation.ArgumentList.Arguments[i].Expression;
                                    var argTypeInfo = semanticModel.GetTypeInfo(argExpr);
                                    if (argTypeInfo.Type?.TypeKind == TypeKind.Dynamic || argTypeInfo.ConvertedType?.TypeKind == TypeKind.Dynamic)
                                        return (true, argExpr.GetLocation());
                                }
                            }
                        }
                    }

                    // Check argument types directly for dynamic
                    foreach (var arg in invocation.ArgumentList.Arguments)
                    {
                        var typeInfo = semanticModel.GetTypeInfo(arg.Expression);
                        if (typeInfo.Type?.TypeKind == TypeKind.Dynamic || typeInfo.ConvertedType?.TypeKind == TypeKind.Dynamic)
                            return (true, arg.Expression.GetLocation());
                    }
                }

                // Check member access on dynamic objects
                if (descendant is MemberAccessExpressionSyntax memberAccess)
                {
                    var exprTypeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
                    if (exprTypeInfo.Type?.TypeKind == TypeKind.Dynamic || exprTypeInfo.ConvertedType?.TypeKind == TypeKind.Dynamic)
                        return (true, memberAccess.GetLocation());
                }

                // Check explicit casts involving dynamic
                if (descendant is CastExpressionSyntax castExpr)
                {
                    var castTypeInfo = semanticModel.GetTypeInfo(castExpr.Type);
                    var exprTypeInfo = semanticModel.GetTypeInfo(castExpr.Expression);
                    if (castTypeInfo.Type?.TypeKind == TypeKind.Dynamic || castTypeInfo.ConvertedType?.TypeKind == TypeKind.Dynamic ||
                        exprTypeInfo.Type?.TypeKind == TypeKind.Dynamic || exprTypeInfo.ConvertedType?.TypeKind == TypeKind.Dynamic)
                        return (true, castExpr.GetLocation());
                }
            }
            return (false, null);
        }

        private bool IsDynamicType(ITypeSymbol? typeSymbol)
        {
            return typeSymbol?.TypeKind == TypeKind.Dynamic;
        }

        // Overload for convenience, assuming typeSyntax is the expression to get location from
        private bool IsDynamicType(TypeSyntax typeSyntax, ExpressionSyntax locationSyntax, SemanticModel semanticModel)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            return IsDynamicType(typeInfo.Type) || IsDynamicType(typeInfo.ConvertedType);
        }
    }
} 