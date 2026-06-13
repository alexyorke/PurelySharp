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
                else if (IsKnownPureArrayFactoryReturn(returnOperation.ReturnedValue, out var factoryMethod))
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
                else if (TryFindReturnedInitializerArrayEscape(
                             returnOperation.ReturnedValue,
                             currentState,
                             out var escapeSyntax,
                             out var escapeSymbol,
                             out var catalogSource))
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned initializer escapes mutable array through '{escapeSyntax}'. Return statement is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        escapeSyntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "mutable_state_escape",
                            ruleName: nameof(ReturnStatementPurityRule),
                            operation: returnOperation,
                            syntaxNode: escapeSyntax,
                            symbol: escapeSymbol,
                            catalogSource: catalogSource));
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [ReturnRule] Returned value is pure. Return statement is Pure.");
                    return valueResult;
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool IsKnownPureArrayFactoryReturn(
            IOperation? returnedValue,
            out IMethodSymbol factoryMethod)
        {
            var unwrappedReturnedValue = PurityAnalysisEngine.UnwrapArrayOwnershipPreservingConversions(returnedValue);
            if (PurityAnalysisEngine.IsKnownPureBCLArrayFactoryOperation(unwrappedReturnedValue, out factoryMethod))
            {
                return true;
            }

            if (unwrappedReturnedValue is IConditionalOperation conditionalOperation)
            {
                if (TryGetConstantCondition(conditionalOperation, out var conditionValue))
                {
                    return IsKnownPureArrayFactoryReturn(
                        conditionValue ? conditionalOperation.WhenTrue : conditionalOperation.WhenFalse,
                        out factoryMethod);
                }

                return IsKnownPureArrayFactoryReturn(conditionalOperation.WhenTrue, out factoryMethod) ||
                    IsKnownPureArrayFactoryReturn(conditionalOperation.WhenFalse, out factoryMethod);
            }

            if (unwrappedReturnedValue is ICoalesceOperation coalesceOperation)
            {
                return IsKnownPureArrayFactoryReturn(coalesceOperation.Value, out factoryMethod) ||
                    IsKnownPureArrayFactoryReturn(coalesceOperation.WhenNull, out factoryMethod);
            }

            factoryMethod = null!;
            return false;
        }

        private static bool IsOwnedLocalArrayReturn(
            IOperation? returnedValue,
            PurityAnalysisEngine.PurityAnalysisState currentState,
            out ILocalSymbol localSymbol)
        {
            var unwrappedReturnedValue = PurityAnalysisEngine.UnwrapArrayOwnershipPreservingConversions(returnedValue);
            if (unwrappedReturnedValue is ILocalReferenceOperation localReference &&
                currentState.IsOwnedLocalArraySymbol(localReference.Local))
            {
                localSymbol = localReference.Local;
                return true;
            }

            if (unwrappedReturnedValue is IConditionalOperation conditionalOperation)
            {
                if (TryGetConstantCondition(conditionalOperation, out var conditionValue))
                {
                    return IsOwnedLocalArrayReturn(
                        conditionValue ? conditionalOperation.WhenTrue : conditionalOperation.WhenFalse,
                        currentState,
                        out localSymbol);
                }

                return IsOwnedLocalArrayReturn(conditionalOperation.WhenTrue, currentState, out localSymbol) ||
                    IsOwnedLocalArrayReturn(conditionalOperation.WhenFalse, currentState, out localSymbol);
            }

            if (unwrappedReturnedValue is ICoalesceOperation coalesceOperation)
            {
                return IsOwnedLocalArrayReturn(coalesceOperation.Value, currentState, out localSymbol) ||
                    IsOwnedLocalArrayReturn(coalesceOperation.WhenNull, currentState, out localSymbol);
            }

            localSymbol = null!;
            return false;
        }

        private static bool TryFindReturnedInitializerArrayEscape(
            IOperation returnedValue,
            PurityAnalysisEngine.PurityAnalysisState currentState,
            out SyntaxNode escapeSyntax,
            out ISymbol escapeSymbol,
            out string catalogSource)
        {
            foreach (var assignment in returnedValue.DescendantsAndSelf().OfType<ISimpleAssignmentOperation>())
            {
                if (IsOwnedLocalArrayReturn(assignment.Value, currentState, out var localSymbol))
                {
                    escapeSyntax = assignment.Value.Syntax;
                    escapeSymbol = localSymbol;
                    catalogSource = "owned_local_array_initializer_escape";
                    return true;
                }

                if (IsKnownPureArrayFactoryReturn(assignment.Value, out var factoryMethod))
                {
                    escapeSyntax = assignment.Value.Syntax;
                    escapeSymbol = factoryMethod;
                    catalogSource = "array_factory_initializer_escape";
                    return true;
                }
            }

            foreach (var objectCreation in returnedValue.DescendantsAndSelf().OfType<IObjectCreationOperation>())
            {
                if (!IsRecordConstructionWithEscapingParameters(objectCreation))
                {
                    continue;
                }

                foreach (var argument in objectCreation.Arguments)
                {
                    if (IsOwnedLocalArrayReturn(argument.Value, currentState, out var localSymbol))
                    {
                        escapeSyntax = argument.Value.Syntax;
                        escapeSymbol = localSymbol;
                        catalogSource = "owned_local_array_record_constructor_escape";
                        return true;
                    }

                    if (IsKnownPureArrayFactoryReturn(argument.Value, out var factoryMethod))
                    {
                        escapeSyntax = argument.Value.Syntax;
                        escapeSymbol = factoryMethod;
                        catalogSource = "array_factory_record_constructor_escape";
                        return true;
                    }
                }
            }

            escapeSyntax = null!;
            escapeSymbol = null!;
            catalogSource = string.Empty;
            return false;
        }

        private static bool IsRecordConstructionWithEscapingParameters(IObjectCreationOperation objectCreationOperation)
        {
            if (objectCreationOperation.Type is not INamedTypeSymbol namedType ||
                !namedType.IsRecord)
            {
                return false;
            }

            foreach (var argument in objectCreationOperation.Arguments)
            {
                var parameter = argument.Parameter;
                if (parameter == null)
                {
                    continue;
                }

                if (HasMatchingRecordProperty(namedType, parameter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMatchingRecordProperty(INamedTypeSymbol recordType, IParameterSymbol parameter)
        {
            foreach (var member in recordType.GetMembers())
            {
                if (member is IPropertySymbol property &&
                    string.Equals(property.Name, parameter.Name, System.StringComparison.OrdinalIgnoreCase) &&
                    SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConstantCondition(IConditionalOperation conditionalOperation, out bool conditionValue)
        {
            if (conditionalOperation.Condition.ConstantValue.HasValue &&
                conditionalOperation.Condition.ConstantValue.Value is bool constantBool)
            {
                conditionValue = constantBool;
                return true;
            }

            conditionValue = false;
            return false;
        }
    }
}
