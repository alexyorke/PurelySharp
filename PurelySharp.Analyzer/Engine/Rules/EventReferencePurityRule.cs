using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

	internal class EventReferencePurityRule : IPurityRule
	{
		public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.EventReference);

		public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
		{
			if (operation is not IEventReferenceOperation eventReference)
			{
				return PurityAnalysisEngine.PurityAnalysisResult.Pure;
			}

			PurityAnalysisEngine.LogDebug($"  [EventRefRule] Checking EventReference: {eventReference.Event?.Name} on {eventReference.Event?.ContainingType?.ToDisplayString()}");

			// Referencing an event value (e.g., `var e = MyEvent;`) is considered pure.
			// Subscriptions/unsubscriptions are handled by EventAssignmentPurityRule via IEventAssignmentOperation.
			return PurityAnalysisEngine.PurityAnalysisResult.Pure;
		}
	}
}


