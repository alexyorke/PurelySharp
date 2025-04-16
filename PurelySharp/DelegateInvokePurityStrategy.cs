using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on whether it's a delegate Invoke method.
    /// Note: This only checks the method kind; runtime instance analysis is needed for full verification.
    /// </summary>
    public class DelegateInvokePurityStrategy : IPurityCheckStrategy
    {
        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's a delegate's Invoke method
            // Considered potentially pure syntactically, runtime check needed for instance
            return method.MethodKind == MethodKind.DelegateInvoke;
        }
    }
} 