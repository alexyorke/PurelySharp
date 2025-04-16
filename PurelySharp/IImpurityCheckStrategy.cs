using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Defines a strategy for checking a specific aspect of method impurity.
    /// </summary>
    public interface IImpurityCheckStrategy
    {
        /// <summary>
        /// Checks if a method meets the impurity criteria defined by this strategy.
        /// </summary>
        /// <param name="method">The method symbol to check.</param>
        /// <returns>True if the method meets the impurity criteria, false otherwise.</returns>
        bool IsImpure(IMethodSymbol method);
    }
} 