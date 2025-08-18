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
					var delegateTargetsBySymbol = new Dictionary<ISymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
					foreach (var inv in body.Descendants().OfType<IInvocationOperation>())
					{
						if (inv.TargetMethod != null)
						{
							callees.Add(inv.TargetMethod.OriginalDefinition);
						}
					}

					foreach (var methodRef in body.Descendants().OfType<IMethodReferenceOperation>())
					{
						if (methodRef.Method != null)
						{
							callees.Add(methodRef.Method.OriginalDefinition);
						}
					}

					foreach (var del in body.Descendants().OfType<IDelegateCreationOperation>())
					{
						if (del.Target is IMethodReferenceOperation target && target.Method != null)
						{
							callees.Add(target.Method.OriginalDefinition);
						}
					}

					foreach (var anon in body.Descendants().OfType<IAnonymousFunctionOperation>())
					{
						if (anon.Symbol != null)
						{
							callees.Add(anon.Symbol.OriginalDefinition);
						}
					}

					// Conservatively add edges for awaited invocations
					foreach (var awaitOp in body.Descendants().OfType<IAwaitOperation>())
					{
						foreach (var awaitedInv in awaitOp.Operation.DescendantsAndSelf().OfType<IInvocationOperation>())
						{
							if (awaitedInv.TargetMethod != null)
							{
								callees.Add(awaitedInv.TargetMethod.OriginalDefinition);
							}
						}
					}

					// Capture delegate assignments and initializations to map symbols -> potential target methods
					foreach (var assignment in body.Descendants().OfType<IAssignmentOperation>())
					{
						var targetSymbol = TryResolveSymbol(assignment.Target);
						if (targetSymbol == null) continue;
						IMethodSymbol? targetMethod = null;
						if (assignment.Value is IMethodReferenceOperation mr1)
						{
							targetMethod = mr1.Method?.OriginalDefinition;
						}
						else if (assignment.Value is IDelegateCreationOperation dc1 && dc1.Target is IMethodReferenceOperation mr2)
						{
							targetMethod = mr2.Method?.OriginalDefinition;
						}
						if (targetMethod != null)
						{
							if (!delegateTargetsBySymbol.TryGetValue(targetSymbol, out var set))
							{
								set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
								delegateTargetsBySymbol[targetSymbol] = set;
							}
							set.Add(targetMethod);
						}
					}

					// Capture delegate compound assignments (e.g., "+=") to accumulate targets
					foreach (var compound in body.Descendants().OfType<ICompoundAssignmentOperation>())
					{
						if (compound.Target?.Type?.TypeKind != TypeKind.Delegate) continue;
						var targetSymbol = TryResolveSymbol(compound.Target);
						if (targetSymbol == null) continue;
						IMethodSymbol? targetMethod = null;
						if (compound.Value is IMethodReferenceOperation mr)
						{
							targetMethod = mr.Method?.OriginalDefinition;
						}
						else if (compound.Value is IDelegateCreationOperation dc && dc.Target is IMethodReferenceOperation mrInner)
						{
							targetMethod = mrInner.Method?.OriginalDefinition;
						}
						if (targetMethod != null)
						{
							if (!delegateTargetsBySymbol.TryGetValue(targetSymbol, out var set))
							{
								set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
								delegateTargetsBySymbol[targetSymbol] = set;
							}
							set.Add(targetMethod);
						}
					}

					// Capture event handler subscriptions (+=) mapping event symbols to potential handler targets
					foreach (var evtAssign in body.Descendants().OfType<IEventAssignmentOperation>())
					{
						var eventSymbol = TryResolveSymbol(evtAssign.EventReference);
						if (eventSymbol == null) continue;
						IMethodSymbol? targetMethod = null;
						if (evtAssign.HandlerValue is IMethodReferenceOperation mr)
						{
							targetMethod = mr.Method?.OriginalDefinition;
						}
						else if (evtAssign.HandlerValue is IDelegateCreationOperation dc && dc.Target is IMethodReferenceOperation mrInner)
						{
							targetMethod = mrInner.Method?.OriginalDefinition;
						}
						if (targetMethod != null)
						{
							if (!delegateTargetsBySymbol.TryGetValue(eventSymbol, out var set))
							{
								set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
								delegateTargetsBySymbol[eventSymbol] = set;
							}
							set.Add(targetMethod);
						}
					}

					foreach (var group in body.Descendants().OfType<IVariableDeclarationGroupOperation>())
					{
						foreach (var decl in group.Declarations)
						{
							foreach (var d in decl.Declarators)
							{
								if (d.Initializer?.Value is IMethodReferenceOperation mr3)
								{
									var sym = d.Symbol;
									if (!delegateTargetsBySymbol.TryGetValue(sym, out var set))
									{
										set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
										delegateTargetsBySymbol[sym] = set;
									}
									set.Add(mr3.Method.OriginalDefinition);
								}
								else if (d.Initializer?.Value is IDelegateCreationOperation dc2 && dc2.Target is IMethodReferenceOperation mr4)
								{
									var sym = d.Symbol;
									if (!delegateTargetsBySymbol.TryGetValue(sym, out var set))
									{
										set = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
										delegateTargetsBySymbol[sym] = set;
									}
									set.Add(mr4.Method.OriginalDefinition);
								}
							}
						}
					}

					// For delegate invocations, add edges to mapped potential targets
					foreach (var inv in body.Descendants().OfType<IInvocationOperation>())
					{
						if (inv.TargetMethod?.Name == "Invoke" && inv.TargetMethod.ContainingType?.TypeKind == TypeKind.Delegate)
						{
							var sym = TryResolveSymbol(inv.Instance);
							if (sym != null && delegateTargetsBySymbol.TryGetValue(sym, out var targets))
							{
								foreach (var t in targets)
								{
									callees.Add(t.OriginalDefinition);
								}
							}
						}
					}
					edges[containingMethod.OriginalDefinition] = System.Collections.Immutable.ImmutableHashSet.CreateRange<IMethodSymbol>(SymbolEqualityComparer.Default, callees);
				}
			}

			return new CallGraph(edges.ToImmutableDictionary(SymbolEqualityComparer.Default));
		}

		private static ISymbol? TryResolveSymbol(IOperation? operation)
		{
			return operation switch
			{
				ILocalReferenceOperation localRef => localRef.Local,
				IParameterReferenceOperation paramRef => paramRef.Parameter,
				IFieldReferenceOperation fieldRef => fieldRef.Field,
				IPropertyReferenceOperation propRef => propRef.Property,
				IEventReferenceOperation eventRef => eventRef.Event,
				_ => null
			};
		}
	}
}

