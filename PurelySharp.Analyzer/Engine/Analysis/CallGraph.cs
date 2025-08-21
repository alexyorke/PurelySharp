using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PurelySharp.Analyzer.Engine.Analysis
{
	internal sealed class CallGraph
	{
		public ImmutableDictionary<IMethodSymbol, ImmutableHashSet<IMethodSymbol>> Edges { get; }

		public CallGraph(ImmutableDictionary<IMethodSymbol, ImmutableHashSet<IMethodSymbol>> edges)
		{
			Edges = edges;
		}
	}
}

