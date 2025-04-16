using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method impurity based on whether it's a property setter (excluding init-only setters).
    /// </summary>
    public class PropertySetterImpurityStrategy : IImpurityCheckStrategy
    {
        public bool IsImpure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Property setters are generally impure (except init-only ones)
            // Note: Relies on the ContainingSymbol being an IPropertySymbol for the init-only check
            return method.MethodKind == MethodKind.PropertySet &&
                   !(method.ContainingSymbol is IPropertySymbol property && property.SetMethod?.IsInitOnly == true);
        }
    }
} 