namespace SharpProof.Worker;
internal static partial class CallableCounterexampleReplayer
{
    internal static WorkerClaimReason Replay(CompilerCallablePreparation target, int claimOrdinal,
        ImmutableDictionary<IrVarId, IrValue> model,
        IReadOnlyList<CompilerPreparedClause> preparedEnsures,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if ((uint)claimOrdinal >= (uint)preparedEnsures.Count)
            {
                return WorkerClaimReason.CounterexampleReplayFailed;
            }

            return CompilerCallablePostconditionReplay.Replay(
                target,
                model,
                preparedEnsures[claimOrdinal].Condition,
                rejectUnexpectedReturnValue: true,
                cancellationToken) switch
            {
                CompilerCallableReplayStatus.Refuted => WorkerClaimReason.None,
                CompilerCallableReplayStatus.PostconditionUndefined =>
                    WorkerClaimReason.PostconditionMayBeUndefined,
                CompilerCallableReplayStatus.UnsupportedRegisteredCall =>
                    WorkerClaimReason.CounterexampleNotReplayable,
                _ => WorkerClaimReason.CounterexampleReplayFailed
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return WorkerClaimReason.CounterexampleReplayFailed;
        }
    }
}
