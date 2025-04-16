using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    /// <summary>
    /// Defines a strategy for checking a specific aspect of method purity.
    /// </summary>
    public interface IPurityCheckStrategy
    {
        /// <summary>
        /// Checks if a method meets the purity criteria defined by this strategy.
        /// </summary>
        /// <param name="method">The method symbol to check.</param>
        /// <returns>True if the method meets the criteria, false otherwise.</returns>
        bool IsPure(IMethodSymbol method);
    }
} 