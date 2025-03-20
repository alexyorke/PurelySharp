using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace PurelySharp.Core
{
    /// <summary>
    /// Tracks information about mutations to a symbol.
    /// </summary>
    public class MutabilityInfo
    {
        /// <summary>
        /// The locations where mutations occur.
        /// </summary>
        public IList<Location> MutationLocations { get; }

        /// <summary>
        /// Whether the symbol is mutated through a reference.
        /// </summary>
        public bool IsMutatedThroughReference { get; set; }

        /// <summary>
        /// Whether the symbol escapes the current scope.
        /// </summary>
        public bool Escapes { get; set; }

        public MutabilityInfo()
        {
            MutationLocations = new List<Location>();
            IsMutatedThroughReference = false;
            Escapes = false;
        }
    }
}