using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on [Pure] or [EnforcePure] attributes.
    /// </summary>
    public class AttributePurityStrategy : IPurityCheckStrategy
    {
        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check for EnforcePure attribute first (as it implies purity check)
            if (method.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name is "EnforcePureAttribute" or "EnforcePure"))
            {
                return true; // Presence of EnforcePure means it *should* be pure, and is handled by the analyzer.
                             // For the purpose of *initial classification* based on attributes, we treat it as pure.
            }

            // Check for Pure attribute
            return method.GetAttributes().Any(a => a.AttributeClass?.Name == "PureAttribute" || a.AttributeClass?.Name == "Pure");
        }
    }
} 