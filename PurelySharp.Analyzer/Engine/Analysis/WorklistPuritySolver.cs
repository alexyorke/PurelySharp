using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PurelySharp.Analyzer.Engine.Analysis
{
	internal static class WorklistPuritySolver
	{
		public static ImmutableDictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult> Solve(
			CallGraph graph,
			Compilation compilation,
			INamedTypeSymbol enforcePureAttributeSymbol,
			INamedTypeSymbol? allowSynchronizationAttributeSymbol)
		{
			var results = new Dictionary<IMethodSymbol, PurityAnalysisEngine.PurityAnalysisResult>(SymbolEqualityComparer.Default);
			var engine = new PurityAnalysisEngine();
			var worklist = new Queue<IMethodSymbol>();

			foreach (var method in graph.Edges.Keys)
			{
				worklist.Enqueue(method);
			}

			while (worklist.Count > 0)
			{
				var method = worklist.Dequeue();
				if (method.DeclaringSyntaxReferences.Length == 0)
				{
					results[method] = PurityAnalysisEngine.PurityAnalysisResult.Pure;
					continue;
				}
				var syntaxRef = method.DeclaringSyntaxReferences[0];
				var model = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
				var purity = engine.IsConsideredPure(method, model, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol);
				if (!results.TryGetValue(method, out var prior) || prior.IsPure != purity.IsPure)
				{
					results[method] = purity;
					if (graph.Edges.TryGetValue(method, out var successors))
					{
						foreach (var succ in successors)
						{
							worklist.Enqueue(succ);
						}
					}
				}
			}

			return results.ToImmutableDictionary(SymbolEqualityComparer.Default);
		}
	}
}

