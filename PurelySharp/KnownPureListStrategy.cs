using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on a predefined list of known pure method signatures.
    /// </summary>
    public class KnownPureListStrategy : IPurityCheckStrategy
    {
        private readonly HashSet<string> _knownPureMethods;

        public KnownPureListStrategy(HashSet<string> knownPureMethods)
        {
            _knownPureMethods = knownPureMethods ?? new HashSet<string>();
        }

        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's a known pure method
            var fullName = method.ContainingType?.ToString() + "." + method.Name;
            return _knownPureMethods.Contains(fullName);
        }
    }
} 