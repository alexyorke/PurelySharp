using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on whether it's an implicit property getter or an init-only property setter.
    /// </summary>
    public class ImplicitGetterOrInitSetterPurityStrategy : IPurityCheckStrategy
    {
        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's a compiler-generated method (e.g., property getter/setter)
            if (method.IsImplicitlyDeclared)
            {
                // Property getters are generally pure
                if (method.MethodKind == MethodKind.PropertyGet)
                    return true;

                // Init-only setters are generally pure (C# 9.0+)
                // Note: Relies on the ContainingSymbol being an IPropertySymbol
                if (method.MethodKind == MethodKind.PropertySet &&
                    method.ContainingSymbol is IPropertySymbol property &&
                    property.SetMethod?.IsInitOnly == true)
                    return true;
            }
            return false;
        }
    }
} 