using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method impurity based on a predefined list of known impure method signatures.
    /// </summary>
    public class KnownImpureListImpurityStrategy : IImpurityCheckStrategy
    {
        private readonly HashSet<string> _knownImpureMethods;

        public KnownImpureListImpurityStrategy(HashSet<string> knownImpureMethods)
        {
            _knownImpureMethods = knownImpureMethods ?? new HashSet<string>();
        }

        public bool IsImpure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's a known impure method
            var fullName = method.ContainingType?.ToString() + "." + method.Name;
            return _knownImpureMethods.Contains(fullName);
        }
    }
} 