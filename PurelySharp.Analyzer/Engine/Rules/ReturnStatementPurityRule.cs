using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ReturnStatementPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Return);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IReturnOperation returnOperation))
            {

                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (returnOperation.ReturnedValue == null)
            {
                PurityAnalysisEngine.LogDebug("    [ReturnRule] No returned value - Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (returnOperation.ReturnedValue != null)
            {
                PurityAnalysisEngine.LogDebug($"    [ReturnRule] Checking returned value: {returnOperation.ReturnedValue.Syntax} ({returnOperation.ReturnedValue.Kind})");
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(returnOperation.ReturnedValue, context, currentState);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value is IMPURE. Return statement is Impure.");
                    return valueResult;
                }
                else if (PurityAnalysisEngine.IsKnownPureBCLArrayFactoryOperation(returnOperation.ReturnedValue, out var factoryMethod))
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value escapes mutable array from known-pure factory '{factoryMethod.ToDisplayString()}'. Return statement is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        returnOperation.ReturnedValue.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "mutable_state_escape",
                            ruleName: nameof(ReturnStatementPurityRule),
                            operation: returnOperation,
                            syntaxNode: returnOperation.ReturnedValue.Syntax,
                            symbol: factoryMethod,
                            catalogSource: "returned_array_factory"));
                }
                else if (IsOwnedLocalArrayReturn(returnOperation.ReturnedValue, currentState, out var localSymbol))
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value escapes owned fresh local array '{localSymbol.Name}'. Return statement is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        returnOperation.ReturnedValue.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "mutable_state_escape",
                            ruleName: nameof(ReturnStatementPurityRule),
                            operation: returnOperation,
                            syntaxNode: returnOperation.ReturnedValue.Syntax,
                            symbol: localSymbol,
                            catalogSource: "owned_local_array_return"));
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value is pure. Return statement is Pure.");
                    return valueResult;
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool IsOwnedLocalArrayReturn(
            IOperation returnedValue,
            PurityAnalysisEngine.PurityAnalysisState currentState,
            out ILocalSymbol localSymbol)
        {
            var unwrappedReturnedValue = PurityAnalysisEngine.SkipImplicitConversions(returnedValue);
            if (unwrappedReturnedValue is ILocalReferenceOperation localReference &&
                localReference.Type is IArrayTypeSymbol &&
                currentState.IsOwnedLocalArraySymbol(localReference.Local))
            {
                localSymbol = localReference.Local;
                return true;
            }

            localSymbol = null!;
            return false;
        }
    }
}
