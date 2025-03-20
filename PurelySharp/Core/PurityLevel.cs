namespace PurelySharp.Core
{
    /// <summary>
    /// Defines different levels of purity requirements.
    /// </summary>
    public enum PurityLevel
    {
        /// <summary>
        /// Strict purity - no side effects, no mutable state, no I/O.
        /// </summary>
        Strict,

        /// <summary>
        /// Allows some controlled mutations like caching.
        /// </summary>
        Relaxed,

        /// <summary>
        /// Only checks for obvious impurities like I/O.
        /// </summary>
        Basic
    }
}