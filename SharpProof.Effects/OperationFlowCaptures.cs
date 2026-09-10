namespace SharpProof.Effects;

internal abstract class OperationFlowCaptures
{
    private readonly FlowCaptureTable<IOperation> _captures =
        new(ManagedFlowResult.HasSameIdentity);

    internal void Record(IFlowCaptureOperation capture)
    {
        if (!IsRelevant(capture))
        {
            return;
        }

        _captures.Record(capture.Id, capture.Value);
    }

    internal IOperation Resolve(IOperation operation)
    {
        var seen = new HashSet<CaptureId>();
        while (operation is IFlowCaptureReferenceOperation capture &&
               seen.Add(capture.Id) &&
               _captures.TryGet(capture.Id, out var captured))
        {
            operation = captured;
        }

        return operation;
    }

    internal bool TryResolve(
        IFlowCaptureReferenceOperation capture,
        out IOperation resolved)
    {
        resolved = Resolve(capture);
        return resolved is not IFlowCaptureReferenceOperation;
    }

    protected abstract bool IsRelevant(IFlowCaptureOperation capture);
}
