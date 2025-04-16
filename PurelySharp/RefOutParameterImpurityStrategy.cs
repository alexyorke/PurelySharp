using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    /// <summary>
    /// Strategy to check method impurity based on the presence of 'ref' or 'out' parameters.
    /// </summary>
    public class RefOutParameterImpurityStrategy : IImpurityCheckStrategy
    {
        public bool IsImpure(IMethodSymbol method)
        {
            if (method == null) return false;

            // Methods with ref or out parameters modify state
            return method.Parameters.Any(p => p.RefKind == RefKind.Ref || p.RefKind == RefKind.Out);
        }
    }
} 