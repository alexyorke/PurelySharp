using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharpProof.Effects;

internal static class PropertyDispatchFacts
{
    internal static bool IsUncertain(
        IPropertyReferenceOperation property,
        IMethodSymbol accessor)
    {
        return !IsStaticallyBound(property) &&
               HasOpenVirtualDispatch(accessor);
    }

    private static bool IsStaticallyBound(
        IPropertyReferenceOperation property)
    {
        return property.Instance?.Syntax is BaseExpressionSyntax ||
               property.Instance?.Type?.IsSealed == true;
    }

    internal static bool HasOpenVirtualDispatch(
        IMethodSymbol accessor,
        bool allowClassReimplementation = false)
    {
        var canReimplement = allowClassReimplementation &&
            accessor.ContainingType?.TypeKind == TypeKind.Class;
        return !accessor.IsStatic &&
               (canReimplement ||
                accessor.IsVirtual ||
                accessor.IsAbstract ||
                accessor.IsOverride ||
                accessor.ContainingType?.TypeKind == TypeKind.Interface) &&
               accessor.ContainingType?.IsSealed != true &&
               (canReimplement || !accessor.IsSealed);
    }
}
