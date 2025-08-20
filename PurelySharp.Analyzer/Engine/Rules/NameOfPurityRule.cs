using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

	internal class NameOfPurityRule : IPurityRule
	{
		public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.NameOf);

		public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
		{
			// nameof is evaluated at compile time; treat as pure.
			PurityAnalysisEngine.LogDebug($"    [NameOfRule] NameOf operation ({operation.Syntax}) - Pure");
			return PurityAnalysisEngine.PurityAnalysisResult.Pure;
		}
	}
}


