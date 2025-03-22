using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PurelySharp
{
    /// <summary>
    /// Provides specialized checking for async methods and await expressions
    /// </summary>
    public static class AsyncPurityChecker
    {
        // Methods from Task and ValueTask classes that are known to be pure
        private static readonly HashSet<string> PureAsyncMethods = new HashSet<string>
        {
            "System.Threading.Tasks.Task.FromResult",
            "System.Threading.Tasks.Task.CompletedTask",
            "System.Threading.Tasks.ValueTask.FromResult",
            "System.Threading.Tasks.ValueTask.CompletedTask",
            "System.Threading.Tasks.Task.FromException",
            "System.Threading.Tasks.Task.FromCanceled"
        };

        // Known impure async methods
        private static readonly HashSet<string> ImpureAsyncMethods = new HashSet<string>
        {
            "System.Threading.Tasks.Task.Delay",
            "System.Threading.Tasks.Task.Run",
            "System.Threading.Tasks.Task.Factory.StartNew",
            "System.Threading.Tasks.Task.WhenAll",
            "System.Threading.Tasks.Task.WhenAny",
            "System.Threading.Tasks.Task.Yield"
        };

        /// <summary>
        /// Checks if the method is an async method that returns Task or ValueTask
        /// </summary>
        public static bool IsAsyncMethod(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            // Check if it's marked with async keyword
            if (methodSymbol.IsAsync)
                return true;

            // Check return type for Task or ValueTask even if not marked async
            var returnTypeName = methodSymbol.ReturnType?.ToString() ?? string.Empty;
            return returnTypeName.StartsWith("System.Threading.Tasks.Task") ||
                   returnTypeName.StartsWith("System.Threading.Tasks.ValueTask");
        }

        /// <summary>
        /// Determines if an await expression is pure
        /// </summary>
        public static bool IsAwaitExpressionPure(AwaitExpressionSyntax awaitExpression, SemanticModel semanticModel)
        {
            if (awaitExpression == null || semanticModel == null)
                return false;

            // Check if the awaited expression is an invocation
            if (awaitExpression.Expression is InvocationExpressionSyntax invocation)
            {
                var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (methodSymbol == null)
                    return false;

                // Check if it's a known pure async method
                var fullMethodName = $"{methodSymbol.ContainingType?.ToString()}.{methodSymbol.Name}";
                if (PureAsyncMethods.Contains(fullMethodName))
                    return true;

                // Special case for Task.CompletedTask property
                if (invocation.ToString().Contains("Task.CompletedTask"))
                    return true;

                // Special case for ValueTask constructors and methods
                if (methodSymbol.ContainingType?.Name == "ValueTask" ||
                    methodSymbol.ReturnType?.Name == "ValueTask")
                    return true;

                // Check if it's a known impure async method
                if (ImpureAsyncMethods.Contains(fullMethodName))
                    return false;

                // Check if we're awaiting a method with [EnforcePure] attribute
                if (HasEnforcePureAttribute(methodSymbol))
                    return true;

                // Special case for pure internal/local methods
                if (methodSymbol.DeclaredAccessibility == Accessibility.Private ||
                    methodSymbol.DeclaredAccessibility == Accessibility.Internal)
                {
                    // For internal methods, trust they are pure unless proven otherwise
                    if (!MethodPurityChecker.IsKnownImpureMethod(methodSymbol))
                        return true;
                }
            }
            // Check for ValueTask constructor
            else if (awaitExpression.Expression is ObjectCreationExpressionSyntax objectCreation)
            {
                var typeInfo = semanticModel.GetTypeInfo(objectCreation);
                var typeName = typeInfo.Type?.ToString() ?? string.Empty;

                if (typeName.StartsWith("System.Threading.Tasks.ValueTask"))
                    return true;
            }
            // Check for member access (e.g. Task.CompletedTask)
            else if (awaitExpression.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol != null)
                {
                    var containingType = symbolInfo.Symbol.ContainingType?.ToString() ?? string.Empty;
                    var memberName = symbolInfo.Symbol.Name;

                    if (containingType.Contains("Task") && memberName == "CompletedTask")
                        return true;

                    if (memberAccess.ToString().Contains("CompletedTask"))
                        return true;
                }
            }
            // Check for identifiers that could be tasks returned from other methods
            else if (awaitExpression.Expression is IdentifierNameSyntax identifier)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                if (symbolInfo.Symbol != null)
                {
                    // If it's a parameter or local variable of Task/ValueTask type, 
                    // assume it can be awaited
                    if (symbolInfo.Symbol is IParameterSymbol ||
                        symbolInfo.Symbol is ILocalSymbol)
                    {
                        var typeInfo = semanticModel.GetTypeInfo(identifier);
                        if (typeInfo.Type?.ToString().Contains("Task") == true)
                            return true;
                    }
                }
            }

            // Default to considering await expressions as potentially impure
            // This is conservative but safer
            return false;
        }

        /// <summary>
        /// Checks if an async method contains only pure operations 
        /// </summary>
        public static bool IsAsyncMethodPure(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
        {
            if (methodSymbol == null || methodDeclaration == null || semanticModel == null)
                return false;

            // First, check if the method is marked as [EnforcePure]
            if (!HasEnforcePureAttribute(methodSymbol))
                return false;

            // Process all local functions in the method to make sure they're also pure
            foreach (var localFunc in methodDeclaration.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var localFuncSymbol = semanticModel.GetDeclaredSymbol(localFunc);

                // Check if the local function is async
                if (localFunc.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
                {
                    // Examine all await expressions in the local function
                    var localAwaitExpressions = localFunc.DescendantNodes().OfType<AwaitExpressionSyntax>();
                    foreach (var awaitExpr in localAwaitExpressions)
                    {
                        if (!IsAwaitExpressionPure(awaitExpr, semanticModel))
                            return false;
                    }
                }
            }

            // Examine all await expressions in the method
            var awaitExpressions = methodDeclaration.DescendantNodes().OfType<AwaitExpressionSyntax>();
            foreach (var awaitExpr in awaitExpressions)
            {
                if (!IsAwaitExpressionPure(awaitExpr, semanticModel))
                    return false;
            }

            // Also check for other potentially impure operations in the method
            // This is handled by the main analyzer

            return true;
        }

        /// <summary>
        /// Determines if a method has the [EnforcePure] attribute
        /// </summary>
        private static bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure");
        }
    }
}