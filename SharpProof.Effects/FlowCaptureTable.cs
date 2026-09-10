namespace SharpProof.Effects;

internal sealed class FlowCaptureTable<T>
{
    private readonly HashSet<CaptureId> _ambiguous = [];
    private readonly Func<T, T, bool> _areEquivalent;
    private readonly Dictionary<CaptureId, T> _values = [];

    internal FlowCaptureTable(Func<T, T, bool> areEquivalent)
    {
        _areEquivalent = areEquivalent;
    }

    internal void Record(CaptureId id, T value)
    {
        if (_values.TryGetValue(id, out var existing))
        {
            if (!_areEquivalent(existing, value))
            {
                _ambiguous.Add(id);
            }

            return;
        }

        _values.Add(id, value);
    }

    internal void MarkAmbiguous(CaptureId id)
    {
        _ambiguous.Add(id);
    }

    internal bool TryGet(CaptureId id, out T value)
    {
        if (_ambiguous.Contains(id))
        {
            value = default!;
            return false;
        }

        return _values.TryGetValue(id, out value);
    }
}
