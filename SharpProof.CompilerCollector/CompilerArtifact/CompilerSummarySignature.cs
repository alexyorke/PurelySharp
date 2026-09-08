namespace SharpProof.CompilerArtifact;

internal static class CompilerSummarySignature
{
    internal static IrSummarySignature Create(
        IrMemberId member, ImmutableArray<IrVarId> parameters,
        IrVarId result, IrSummaryProvenance provenance)
    {
        return new(member, receiver: null, parameters, result, provenance);
    }
}
