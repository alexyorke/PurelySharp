namespace SharpProof.CompilerArtifact;

internal static class CompilerSummarySignature
{
    internal static IrSummarySignature Create(
        IrMemberId member, ImmutableArray<IrVarId> parameters,
        IrVarId result, IrSummaryProvenance provenance) =>
        new(member, receiver: null, parameters, result, provenance);
}
