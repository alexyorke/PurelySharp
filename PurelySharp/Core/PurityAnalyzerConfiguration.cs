using System.Collections.Generic;

namespace PurelySharp.Core
{
    /// <summary>
    /// Configuration options for purity analysis.
    /// </summary>
    public class PurityAnalyzerConfiguration
    {
        /// <summary>
        /// The default purity level to use when not specified.
        /// </summary>
        public PurityLevel DefaultLevel { get; set; } = PurityLevel.Strict;

        /// <summary>
        /// Whether to enable incremental analysis.
        /// </summary>
        public bool EnableIncrementalAnalysis { get; set; } = false;

        /// <summary>
        /// Custom settings for the analyzer.
        /// </summary>
        public IDictionary<string, string> CustomSettings { get; }

        /// <summary>
        /// Whether to allow lock statements in pure methods.
        /// </summary>
        public bool AllowLockStatements { get; set; } = false;

        /// <summary>
        /// Whether to treat string operations as pure.
        /// </summary>
        public bool TreatStringOperationsAsPure { get; set; } = true;

        /// <summary>
        /// Whether to allow pure methods to allocate memory.
        /// </summary>
        public bool AllowMemoryAllocation { get; set; } = true;

        public PurityAnalyzerConfiguration()
        {
            CustomSettings = new Dictionary<string, string>();
        }
    }
}