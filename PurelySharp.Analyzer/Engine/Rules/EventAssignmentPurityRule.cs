using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

	internal class EventAssignmentPurityRule : IPurityRule
	{
		public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.EventAssignment);

		public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
		{
			if (operation is not IEventAssignmentOperation eventAssignment)
			{
				return PurityAnalysisEngine.PurityAnalysisResult.Pure;
			}

			PurityAnalysisEngine.LogDebug($"  [EventAssignRule] Checking event assignment: {eventAssignment.Syntax}");

			// Subscribing or unsubscribing to an event mutates the event's invocation list (stateful) => impure.
			return PurityAnalysisEngine.PurityAnalysisResult.Impure(eventAssignment.Syntax);
		}
	}
}


