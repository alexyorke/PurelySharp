using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on whether it's a built-in operator or a conversion method.
    /// </summary>
    public class BuiltinOperatorOrConversionPurityStrategy : IPurityCheckStrategy
    {
        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Basic operators (+, -, *, /, etc.) are generally pure
            if (method.MethodKind == MethodKind.BuiltinOperator)
                return true;

            // Check if it's a conversion method (MethodKind.Conversion), which are generally pure
            return method.MethodKind == MethodKind.Conversion;
        }
    }
} 