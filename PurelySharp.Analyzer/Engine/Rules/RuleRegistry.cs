using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
	internal static class RuleRegistry
	{
		public static ImmutableList<IPurityRule> GetDefaultRules()
		{
			// Group by construct families for clarity of ordering (keep stable behavior)
			return ImmutableList.Create<IPurityRule>(
				// Invocation/Calls (keep primary method invocation rule first)
				new MethodInvocationPurityRule(),
				new ConstructorInitializerPurityRule(),
				new DelegateCreationPurityRule(),
				new AwaitPurityRule(),
				
				// Assignments/References
				new AssignmentPurityRule(),
				new ExpressionStatementPurityRule(),
				new ParameterReferencePurityRule(),
				new LocalReferencePurityRule(),
				new FieldReferencePurityRule(),
				new InstanceReferencePurityRule(),
				
				// Object/Array creation and initialization
				new ObjectCreationPurityRule(),
				new ObjectOrCollectionInitializerPurityRule(),
				new ArrayCreationPurityRule(),
				new ArrayInitializerPurityRule(),
				new ArrayElementReferencePurityRule(),
				new CollectionExpressionPurityRule(),
				
				// Expressions/Operators
				new BinaryOperationPurityRule(),
				new UnaryOperationPurityRule(),
				new CoalesceOperationPurityRule(),
				new ConditionalAccessPurityRule(),
				new ConditionalOperationPurityRule(),
				new ConversionPurityRule(),
				new DefaultValuePurityRule(),
				new InterpolatedStringPurityRule(),
				new PropertyReferencePurityRule(),
				new LiteralPurityRule(),
				new TuplePurityRule(),
				new TypeOfPurityRule(),
				new Utf8StringLiteralPurityRule(),
				new SizeOfPurityRule(),
				
				// Patterns
				new BinaryPatternPurityRule(),
				new ConstantPatternPurityRule(),
				new DeclarationPatternPurityRule(),
				new DiscardPatternPurityRule(),
				new IsPatternPurityRule(),
				new IsNullPurityRule(),
				new StructuralPurityRule(),
				
				// Control Flow
				new BranchPurityRule(),
				new SwitchStatementPurityRule(),
				new SwitchCasePurityRule(),
				new SwitchExpressionPurityRule(),
				new LoopPurityRule(),
				new UsingStatementPurityRule(),
				new ThrowOperationPurityRule(),
				new LockStatementPurityRule(),
				new YieldReturnPurityRule(),
				
				// Flow/CFG helpers
				new FlowCapturePurityRule(),
				new FlowCaptureReferencePurityRule(),
				
				// Returns
				new ReturnStatementPurityRule(),
				
				// Misc
				new WithOperationPurityRule()
			);
		}
	}
}

