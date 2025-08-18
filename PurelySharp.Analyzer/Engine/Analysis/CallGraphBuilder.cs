using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace PurelySharp.Analyzer.Engine.Analysis
{
	internal static class CallGraphBuilder
	{
		public static CallGraph Build(Compilation compilation)
		{
			var edges = new Dictionary<IMethodSymbol, ImmutableHashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);

			foreach (var tree in compilation.SyntaxTrees)
			{
				var model = compilation.GetSemanticModel(tree);
				var root = tree.GetRoot();
				var operations = root.DescendantNodes().Select(n => model.GetOperation(n)).OfType<IMethodBodyOperation>();
				foreach (var body in operations)
				{
					var containingMethod = model.GetEnclosingSymbol(body.Syntax.SpanStart) as IMethodSymbol;
					if (containingMethod == null) continue;
					var callees = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
					foreach (var inv in body.Descendants().OfType<IInvocationOperation>())
					{
						if (inv.TargetMethod != null)
						{
							callees.Add(inv.TargetMethod.OriginalDefinition);
						}
					}
					edges[containingMethod.OriginalDefinition] = callees.ToImmutableHashSet();
				}
			}

			return new CallGraph(edges.ToImmutableDictionary(SymbolEqualityComparer.Default));
		}
	}
}

