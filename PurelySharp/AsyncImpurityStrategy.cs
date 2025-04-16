using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method impurity based on whether it is an async method.
    /// Async methods are considered impure by default unless they have the [EnforcePure] attribute.
    /// </summary>
    public class AsyncImpurityStrategy : IImpurityCheckStrategy
    {
        public bool IsImpure(IMethodSymbol method)
        {
            if (method == null || !method.IsAsync)
            {
                return false; // Not impure based on the async rule alone.
            }

            // If it's async, check for the EnforcePure attribute.
            // If the attribute IS present, the analyzer handles it separately, so it's NOT considered impure by this default rule.
            // If the attribute IS NOT present, we default to treating it as impure.
            return !HasEnforcePureAttribute(method);
        }

        // Helper function to check for EnforcePure attribute (mirrors the one in MethodPurityChecker for now)
        // Ideally, this dependency should be injected or handled differently in a larger refactoring.
        private static bool HasEnforcePureAttribute(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return false;

            return methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure");
        }
    }
} 