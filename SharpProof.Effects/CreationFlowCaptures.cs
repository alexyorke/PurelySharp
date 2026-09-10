namespace SharpProof.Effects;

internal sealed class CreationFlowCaptures
{
    private readonly FlowCaptureTable<EffectRegionSet> _captures =
        new(static (left, right) => left == right);

    internal void Record(IFlowCaptureOperation capture)
    {
        var value = capture.Value;
        while (value is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion)
        {
            value = conversion.Operand;
        }

        if (value is not (IObjectCreationOperation or IArrayCreationOperation))
        {
            // A later definition can reuse this capture ID at a control-flow
            // merge. Remember non-creation provenance even when no fresh
            // definition has been seen yet.
            _captures.MarkAmbiguous(capture.Id);
            return;
        }

        var region = EffectRegionSet.Create(
            EffectRegionId.Fresh(value.Syntax.SpanStart));
        _captures.Record(capture.Id, region);
    }

    internal bool TryResolve(
        IFlowCaptureReferenceOperation capture,
        out EffectRegionSet region)
    {
        if (_captures.TryGet(capture.Id, out region))
        {
            return true;
        }

        region = EffectRegionSet.Unknown;
        return false;
    }
}
