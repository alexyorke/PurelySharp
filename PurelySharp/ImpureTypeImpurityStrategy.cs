using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method impurity based on whether its containing type is in a predefined list of impure types.
    /// </summary>
    public class ImpureTypeImpurityStrategy : IImpurityCheckStrategy
    {
        private readonly HashSet<string> _impureTypes;

        public ImpureTypeImpurityStrategy(HashSet<string> impureTypes)
        {
            _impureTypes = impureTypes ?? new HashSet<string>();
        }

        public bool IsImpure(IMethodSymbol method)
        {
            if (method?.ContainingType == null) return false;

            // Check if it's in an impure type
            return _impureTypes.Contains(method.ContainingType.ToString());
        }
    }
} 