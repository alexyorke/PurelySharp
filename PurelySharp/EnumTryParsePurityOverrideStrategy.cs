using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to identify the specific case of System.Enum.TryParse methods.
    /// These are considered pure despite having an 'out' parameter, so they represent an exception to the general impurity rules.
    /// </summary>
    public class EnumTryParsePurityOverrideStrategy : IImpurityCheckStrategy // Naming reflects its purpose
    {
        public bool IsImpure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's the specific System.Enum.TryParse method
            return method.Name == "TryParse" &&
                   method.ContainingType?.Name == "Enum" &&
                   method.ContainingType.ContainingNamespace?.Name == "System";
        }

        // Note: The caller (MethodPurityChecker) needs to interpret the 'true' result
        // from this strategy as a reason to consider the method *not* impure, overriding other checks.
    }
} 