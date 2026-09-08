namespace SharpProof.Effects;

internal abstract class OperationFlowCaptures
{
    private readonly HashSet<CaptureId> _ambiguous = [];
    private readonly Dictionary<CaptureId, IOperation> _capturedValues = [];

    internal void Record(IFlowCaptureOperation capture)
    {
        if (!IsRelevant(capture))
        {
            return;
        }

        if (_capturedValues.TryGetValue(capture.Id, out var existing))
        {
            if (!ManagedFlowResult.HasSameIdentity(existing, capture.Value))
            {
                _ambiguous.Add(capture.Id);
            }

            return;
        }

        _capturedValues.Add(capture.Id, capture.Value);
    }

    internal IOperation Resolve(IOperation operation)
    {
        var seen = new HashSet<CaptureId>();
        while (operation is IFlowCaptureReferenceOperation capture &&
               seen.Add(capture.Id) &&
               !_ambiguous.Contains(capture.Id) &&
               _capturedValues.TryGetValue(capture.Id, out var captured))
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
