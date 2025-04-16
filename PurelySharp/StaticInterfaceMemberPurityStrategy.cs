using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on its relationship to static interface members.
    /// - Static interface member definitions are considered pure *within the interface context*.
    /// - Implementations/overrides of static interface members are *not* considered known pure by default.
    /// </summary>
    public class StaticInterfaceMemberPurityStrategy : IPurityCheckStrategy
    {
        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check for static virtual/abstract interface members (definition)
            if (method.IsStatic &&
                method.ContainingType?.TypeKind == TypeKind.Interface &&
                (method.IsVirtual || method.IsAbstract))
            {
                // Static interface member definitions are considered pure *within the interface* context
                return true;
            }

            // Check for implementations/overrides of static virtual/abstract interface members
            // We consider the *implementation* potentially impure unless marked pure.
            // The original logic returned false here, meaning it didn't classify it as known pure.
            // Keeping original logic: if it's an override of static interface member, it's NOT known pure by default.
            if (method.IsStatic && method.IsOverride)
            {
                var overriddenMethod = method.OverriddenMethod;
                if (overriddenMethod != null &&
                    overriddenMethod.ContainingType?.TypeKind == TypeKind.Interface &&
                    (overriddenMethod.IsVirtual || overriddenMethod.IsAbstract)) // Check if overridden is static virt/abs
                {
                    // This method implements/overrides a static interface member.
                    // We don't automatically consider it pure; requires attribute or further analysis.
                    return false; // Explicitly not known pure based on this rule alone.
                }
            }

            // If none of the above conditions related to static interface members apply, this rule doesn't make it pure.
            return false;
        }
    }
} 