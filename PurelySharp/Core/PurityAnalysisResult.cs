using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp.Core
{
    /// <summary>
    /// Result of a purity analysis.
    /// </summary>
    public class PurityAnalysisResult
    {
        /// <summary>
        /// Whether the analyzed code is pure.
        /// </summary>
        public bool IsPure { get; }

        /// <summary>
        /// Locations where impurities were found.
        /// </summary>
        public IReadOnlyList<Location> ImpurityLocations { get; }

        /// <summary>
        /// Descriptions of why each location is impure.
        /// </summary>
        public IReadOnlyDictionary<Location, string> ImpurityReasons { get; }

        public PurityAnalysisResult(bool isPure, IEnumerable<Location>? impurityLocations = null, IDictionary<Location, string>? impurityReasons = null)
        {
            IsPure = isPure;
            ImpurityLocations = (impurityLocations ?? Enumerable.Empty<Location>()).ToList();
            ImpurityReasons = new Dictionary<Location, string>(impurityReasons ?? new Dictionary<Location, string>());
        }

        /// <summary>
        /// Creates a pure result.
        /// </summary>
        public static PurityAnalysisResult Pure() => new PurityAnalysisResult(true);

        /// <summary>
        /// Creates an impure result with a single location and reason.
        /// </summary>
        public static PurityAnalysisResult Impure(Location location, string reason)
        {
            return new PurityAnalysisResult(
                false,
                new[] { location },
                new Dictionary<Location, string> { { location, reason } });
        }

        /// <summary>
        /// Creates an impure result with multiple locations and reasons.
        /// </summary>
        public static PurityAnalysisResult Impure(IEnumerable<Location> locations, IDictionary<Location, string> reasons)
        {
            return new PurityAnalysisResult(false, locations, reasons);
        }

        /// <summary>
        /// Combines multiple purity analysis results into a single result.
        /// </summary>
        public static PurityAnalysisResult Combine(params PurityAnalysisResult[] results)
        {
            if (results.Length == 0)
                return Pure();

            if (results.All(r => r.IsPure))
                return Pure();

            var allLocations = results.SelectMany(r => r.ImpurityLocations).ToList();
            var allReasons = results.SelectMany(r => r.ImpurityReasons)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return new PurityAnalysisResult(false, allLocations, allReasons);
        }
    }
}