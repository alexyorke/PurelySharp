using Microsoft.CodeAnalysis;

namespace PurelySharp.AnalyzerStrategies
{
    /// <summary>
    /// Represents the result of a specific purity check performed by a strategy.
    /// </summary>
    public class CheckPurityResult
    {
        /// <summary>
        /// Indicates whether the check passed (i.e., no impurity related to this check was found).
        /// Note: A result of 'true' does not mean the entire method is pure, only that this specific check found no issues.
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// The location of the impurity if Passed is false.
        /// </summary>
        public Location? ImpurityLocation { get; }

        /// <summary>
        /// A description of the impurity found, if any.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Gets a singleton instance representing a passed check.
        /// </summary>
        public static CheckPurityResult Pass { get; } = new CheckPurityResult(true, null, null);

        private CheckPurityResult(bool passed, Location? impurityLocation, string? reason)
        {
            Passed = passed;
            ImpurityLocation = impurityLocation;
            Reason = reason;
        }

        /// <summary>
        /// Creates a failure result.
        /// </summary>
        public static CheckPurityResult Fail(Location location, string reason)
        {
            return new CheckPurityResult(false, location, reason);
        }
    }
} 