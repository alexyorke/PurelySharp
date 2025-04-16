using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method purity based on whether it belongs to a known pure namespace (e.g., System.Linq, System.Collections.Immutable)
    /// or if it's a LINQ extension method.
    /// </summary>
    public class PureNamespaceOrLinqExtensionStrategy : IPurityCheckStrategy
    {
        private readonly HashSet<string> _pureNamespaces;

        public PureNamespaceOrLinqExtensionStrategy(HashSet<string> pureNamespaces)
        {
            _pureNamespaces = pureNamespaces ?? new HashSet<string>();
        }

        public bool IsPure(IMethodSymbol method)
        {
            if (method == null) return false;

            var namespaceName = method.ContainingNamespace?.ToString() ?? string.Empty;

            // Check if it's from a pure namespace
            if (_pureNamespaces.Any(ns => namespaceName.StartsWith(ns)))
                return true;

            // LINQ extension methods are pure (check namespace specifically for LINQ)
            return method.IsExtensionMethod && namespaceName.StartsWith("System.Linq");
        }
    }
} 