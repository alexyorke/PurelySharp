using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
	internal static class RuleRegistry
	{
		public static ImmutableList<IPurityRule> GetDefaultRules()
		{
			return ImmutableList.Create<IPurityRule>(
				new AssignmentPurityRule(),
				new MethodInvocationPurityRule(),
				new ConstructorInitializerPurityRule(),
				new ReturnStatementPurityRule(),
				new BinaryOperationPurityRule(),
				new BinaryPatternPurityRule(),
				new PropertyReferencePurityRule(),
				new ArrayElementReferencePurityRule(),
				new CollectionExpressionPurityRule(),
				new ArrayCreationPurityRule(),
				new ArrayInitializerPurityRule(),
				new InterpolatedStringPurityRule(),
				new SwitchStatementPurityRule(),
				new SwitchExpressionPurityRule(),
				new ConstantPatternPurityRule(),
				new DeclarationPatternPurityRule(),
				new DiscardPatternPurityRule(),
				new LoopPurityRule(),
				new FlowCapturePurityRule(),
				new ExpressionStatementPurityRule(),
				new UsingStatementPurityRule(),
				new ParameterReferencePurityRule(),
				new LocalReferencePurityRule(),
				new FieldReferencePurityRule(),
				new BranchPurityRule(),
				new SwitchCasePurityRule(),
				new LiteralPurityRule(),
				new ConversionPurityRule(),
				new DefaultValuePurityRule(),
				new FlowCaptureReferencePurityRule(),
				new ConditionalOperationPurityRule(),
				new UnaryOperationPurityRule(),
				new ObjectCreationPurityRule(),
				new CoalesceOperationPurityRule(),
				new ConditionalAccessPurityRule(),
				new ThrowOperationPurityRule(),
				new VariableDeclarationGroupPurityRule(),
				new IsPatternPurityRule(),
				new IsNullPurityRule(),
				new StructuralPurityRule(),
				new TuplePurityRule(),
				new TypeOfPurityRule(),
				new YieldReturnPurityRule(),
				new DelegateCreationPurityRule(),
				new WithOperationPurityRule(),
				new InstanceReferencePurityRule(),
				new ObjectOrCollectionInitializerPurityRule(),
				new LockStatementPurityRule(),
				new AwaitPurityRule(),
				new Utf8StringLiteralPurityRule(),
				new SizeOfPurityRule()
			);
		}
	}
}

