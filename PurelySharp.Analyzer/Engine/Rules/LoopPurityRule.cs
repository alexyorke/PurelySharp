using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class LoopPurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Loop);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is ILoopOperation loopOperation))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: LoopPurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure for safety.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            PurityAnalysisEngine.LogDebug($"    [LoopRule] Analyzing loop body for: {loopOperation.Syntax}");

            if (loopOperation is IForLoopOperation forLoopOperation)
            {
                foreach (var beforeOperation in forLoopOperation.Before)
                {
                    var beforeResult = PurityAnalysisEngine.CheckSingleOperation(beforeOperation, context, currentState);
                    if (!beforeResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to for-loop initializer: {beforeOperation.Syntax}");
                        return beforeResult;
                    }
                }

                if (forLoopOperation.Condition != null)
                {
                    var conditionResult = PurityAnalysisEngine.CheckSingleOperation(forLoopOperation.Condition, context, currentState);
                    if (!conditionResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to for-loop condition: {forLoopOperation.Condition.Syntax}");
                        return conditionResult;
                    }
                }
            }
            else if (loopOperation is IWhileLoopOperation whileLoopOperation &&
                whileLoopOperation.Condition != null)
            {
                var conditionResult = PurityAnalysisEngine.CheckSingleOperation(whileLoopOperation.Condition, context, currentState);
                if (!conditionResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to while-loop condition: {whileLoopOperation.Condition.Syntax}");
                    return conditionResult;
                }
            }

            if (HasStaticallyUnreachableBody(loopOperation))
            {
                PurityAnalysisEngine.LogDebug($"    [LoopRule] Skipping unreachable loop body for: {loopOperation.Syntax}");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            if (loopOperation is IForEachLoopOperation forEachLoopOperation)
            {
                var collectionResult = PurityAnalysisEngine.CheckSingleOperation(forEachLoopOperation.Collection, context, currentState);
                if (!collectionResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to foreach collection expression: {forEachLoopOperation.Collection.Syntax}");
                    return collectionResult;
                }

                var enumeratorResult = CheckForEachEnumeratorPurity(forEachLoopOperation.Collection, context);
                if (!enumeratorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to foreach GetEnumerator implementation: {forEachLoopOperation.Collection.Syntax}");
                    return enumeratorResult;
                }
            }


            if (loopOperation.Body != null)
            {
                foreach (var bodyOp in loopOperation.Body.DescendantsAndSelf())
                {

                    var opResult = PurityAnalysisEngine.CheckSingleOperation(bodyOp, context, currentState);
                    if (!opResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to operation in loop body: {bodyOp.Kind} at {bodyOp.Syntax.GetLocation()?.GetLineSpan().StartLinePosition}");
                        return opResult;
                    }
                }
            }

            if (loopOperation is IForLoopOperation reachableForLoopOperation)
            {
                foreach (var atLoopBottomOperation in reachableForLoopOperation.AtLoopBottom)
                {
                    var atLoopBottomResult = PurityAnalysisEngine.CheckSingleOperation(atLoopBottomOperation, context, currentState);
                    if (!atLoopBottomResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [LoopRule] IMPURE due to for-loop incrementor: {atLoopBottomOperation.Syntax}");
                        return atLoopBottomResult;
                    }
                }
            }




            PurityAnalysisEngine.LogDebug($"    [LoopRule] Loop body analyzed as pure for: {loopOperation.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool HasStaticallyUnreachableBody(ILoopOperation loopOperation)
        {
            return loopOperation switch
            {
                IWhileLoopOperation whileLoop => whileLoop.ConditionIsTop && IsCompileTimeFalse(whileLoop.Condition),
                IForLoopOperation forLoop => IsCompileTimeFalse(forLoop.Condition),
                _ => false
            };
        }

        private static bool IsCompileTimeFalse(IOperation? conditionOperation)
        {
            if (conditionOperation == null)
            {
                return false;
            }

            var constantValue = conditionOperation.ConstantValue;
            return constantValue.HasValue &&
                   constantValue.Value is bool boolValue &&
                   !boolValue;
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckForEachEnumeratorPurity(
            IOperation collectionOperation,
            PurityAnalysisContext context)
        {
            var unwrappedCollection = PurityAnalysisEngine.SkipImplicitConversions(collectionOperation) ?? collectionOperation;
            if (unwrappedCollection.Type == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            foreach (var getEnumerator in EnumerateGetEnumeratorImplementations(unwrappedCollection.Type))
            {
                var enumeratorPurity = PurityAnalysisEngine.GetCalleePurity(getEnumerator.OriginalDefinition, context);
                if (!enumeratorPurity.IsPure)
                {
                    return enumeratorPurity.WithCallee(getEnumerator, unwrappedCollection.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static IEnumerable<IMethodSymbol> EnumerateGetEnumeratorImplementations(ITypeSymbol collectionType)
        {
            var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            foreach (var getEnumerator in collectionType
                         .GetMembers("GetEnumerator")
                         .OfType<IMethodSymbol>()
                         .Where(method => method.Parameters.Length == 0 && method.DeclaringSyntaxReferences.Length > 0))
            {
                if (seen.Add(getEnumerator.OriginalDefinition))
                {
                    yield return getEnumerator;
                }
            }

            if (collectionType is not INamedTypeSymbol namedCollectionType)
            {
                yield break;
            }

            foreach (var interfaceType in namedCollectionType.AllInterfaces)
            {
                if (!IsEnumerableInterface(interfaceType))
                {
                    continue;
                }

                foreach (var interfaceGetEnumerator in interfaceType
                             .GetMembers("GetEnumerator")
                             .OfType<IMethodSymbol>()
                             .Where(method => method.Parameters.Length == 0))
                {
                    var implementation = namedCollectionType.FindImplementationForInterfaceMember(interfaceGetEnumerator) as IMethodSymbol;
                    if (implementation == null || implementation.DeclaringSyntaxReferences.Length == 0)
                    {
                        continue;
                    }

                    if (seen.Add(implementation.OriginalDefinition))
                    {
                        yield return implementation;
                    }
                }
            }
        }

        private static bool IsEnumerableInterface(INamedTypeSymbol typeSymbol)
        {
            var originalDefinition = typeSymbol.OriginalDefinition;
            return originalDefinition.SpecialType == SpecialType.System_Collections_IEnumerable ||
                originalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }
    }
}
