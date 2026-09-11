using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace SharpProof.Roslyn;

internal static class RoslynCfgThrowFacts
{
    internal static IEnumerable<BasicBlock> ReachableBlocks(
        ControlFlowGraph graph,
        CancellationToken cancellationToken = default)
    {
        foreach (var block in graph.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (block.IsReachable)
            {
                yield return block;
            }
        }
    }

    internal static bool BuiltInOperationMayThrow(IOperation operation)
    {
        return operation is
            IInvocationOperation or
            IDynamicInvocationOperation or
            IFunctionPointerInvocationOperation or
            IObjectCreationOperation or
            IArrayCreationOperation or
            IArrayElementReferenceOperation or
            IPropertyReferenceOperation or
            ILockOperation or
            IConversionOperation
            { IsChecked: true, OperatorMethod: null } or
            ICompoundAssignmentOperation
            {
                IsChecked: true,
                OperatorMethod: null
            } or
            ICompoundAssignmentOperation
            {
                OperatorMethod: null,
                OperatorKind: BinaryOperatorKind.Divide or
                    BinaryOperatorKind.Remainder
            } or
            IBinaryOperation
            {
                IsChecked: true,
                OperatorMethod: null
            } or
            IBinaryOperation
            {
                OperatorMethod: null,
                OperatorKind: BinaryOperatorKind.Divide or
                    BinaryOperatorKind.Remainder
            } or
            IUnaryOperation
            { IsChecked: true, OperatorMethod: null } or
            IIncrementOrDecrementOperation
            { IsChecked: true, OperatorMethod: null };
    }

    internal static bool OperationMayThrow(IOperation operation)
    {
        if (operation is IConversionOperation conversion)
        {
            return conversion.OperatorMethod != null ||
                conversion.IsChecked ||
                (!conversion.IsTryCast && !conversion.IsImplicit &&
                 (conversion.Conversion.IsReference ||
                  conversion.Operand.Type?.IsReferenceType == true &&
                  conversion.Type?.IsValueType == true));
        }
        if (operation is IMethodReferenceOperation methodReference)
        {
            return !methodReference.Method.IsStatic &&
                methodReference.Instance?.Type?.IsReferenceType == true;
        }
        return BuiltInOperationMayThrow(operation) ||
            operation is
                IThrowOperation or
                IDynamicObjectCreationOperation or
                IDynamicIndexerAccessOperation or
                IDynamicMemberReferenceOperation or
                IFieldReferenceOperation { Instance: not null } or
                IEventAssignmentOperation or
                IAwaitOperation or
                ICompoundAssignmentOperation { OperatorMethod: not null } or
                IBinaryOperation { OperatorMethod: not null } or
                IUnaryOperation { OperatorMethod: not null } or
                IIncrementOrDecrementOperation { OperatorMethod: not null };
    }

    internal static IEnumerable<BasicBlock> ExceptionalSuccessors(
        ControlFlowGraph graph,
        BasicBlock block,
        CancellationToken cancellationToken = default)
    {
        var yielded = new HashSet<int>();
        for (var region = block.EnclosingRegion;
             region != null;
             region = region.EnclosingRegion)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (region.Kind != ControlFlowRegionKind.Try ||
                region.EnclosingRegion is not { } owner)
            {
                continue;
            }

            foreach (var handler in owner.NestedRegions.Where(candidate =>
                         candidate.Kind is ControlFlowRegionKind.Filter or
                             ControlFlowRegionKind.Catch or
                             ControlFlowRegionKind.FilterAndHandler or
                             ControlFlowRegionKind.Finally))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (yielded.Add(handler.FirstBlockOrdinal))
                {
                    yield return graph.Blocks[handler.FirstBlockOrdinal];
                }
            }
        }
    }
}
