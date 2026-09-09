using SharpProof.Ir;
using SharpProof.Worker.Protocol;

namespace SharpProof.CompilerArtifact;

internal static class CompilerEffectClaimArtifactCodec
{
    internal static void Seal(CompilerEffectClaimArtifact value)
    {
        if (value.Replay is { } replay)
        {
            replay.ConstraintSha256 = ComputeConstraintSha256(value.ContractKind, value.Constraint);
            using var hash = StartEvidenceHash(value);
            foreach (var effectEvent in replay.Events ?? [])
            {
                effectEvent.OperationIdentitySha256 = ComputeReplayOperationSha256(effectEvent);
                AddReplayEvent(hash, effectEvent, includeOrdinal: true, includeOperationIdentity: true);
            }
            value.EvidenceSha256 = FinishEvidenceHash(hash, value);
            return;
        }
        value.EvidenceSha256 = ComputeSha256(value);
    }

    internal static void Validate(CompilerEffectClaimArtifact value)
    {
        Validate(value, null);
    }

    internal static void Validate(
        CompilerEffectClaimArtifact value,
        CompilerCompilationSnapshot? compilation)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ClaimId) ||
            string.IsNullOrWhiteSpace(value.Evidence) ||
            !WorkerProtocolJson.IsDefined(value.ContractKind, WorkerEffectContractKind.Unspecified) ||
            !Enum.IsDefined(typeof(WorkerClaimReason), value.Reason) ||
            !WorkerProtocolJson.IsDefined(value.Certainty, WorkerEffectEvidenceCertainty.Unspecified) ||
            !HasValidConstraint(value.ContractKind, value.Constraint))
        {
            throw new InvalidDataException("Compiler effect-claim evidence is invalid.");
        }

        if (!TryValidateAndComputeEvidenceSha256(
                value,
                out var expectedEvidenceSha256) ||
            !HasValidOutcome(value) ||
            value.EvidenceSha256 != expectedEvidenceSha256 ||
            (compilation != null && !HasValidReplayGeometry(value, compilation)))
        {
            throw new InvalidDataException("Compiler effect-claim evidence is invalid.");
        }
    }

    internal static bool HasValidReplayGeometry(
        CompilerEffectClaimArtifact? value,
        CompilerCompilationSnapshot? compilation)
    {
        if (value?.Replay == null)
        {
            return true;
        }

        if (compilation is not { SyntaxTrees: not null })
        {
            return false;
        }

        foreach (var effectEvent in value.Replay.Events ?? [])
        {
            if (effectEvent == null ||
                effectEvent.SyntaxTreeOrdinal < 0 ||
                effectEvent.SyntaxTreeOrdinal >= compilation.SyntaxTrees.Length)
            {
                return false;
            }

            var syntaxTree = compilation.SyntaxTrees[effectEvent.SyntaxTreeOrdinal];
            if (syntaxTree == null ||
                effectEvent.SyntaxTreeSha256 != syntaxTree.Sha256 ||
                effectEvent.SyntaxTreeSnapshotSha256 !=
                    CompilationFingerprint.ComputeSyntaxTreeSnapshotSha256(syntaxTree) ||
                effectEvent.SyntaxTreeLineMapSha256 != syntaxTree.LineMapSha256 ||
                effectEvent.SyntaxStart < 0 ||
                effectEvent.SyntaxLength <= 0 ||
                effectEvent.SyntaxStart > syntaxTree.TextLength ||
                effectEvent.SyntaxLength >
                    syntaxTree.TextLength - effectEvent.SyntaxStart)
            {
                return false;
            }

            if (CompilerSourceLocationAuthority.FindUniqueTree(
                    effectEvent.Location,
                    compilation) != effectEvent.SourceTreeOrdinal ||
                !CompilerSourceLocationAuthority.IsBound(
                    effectEvent.Location,
                    effectEvent.SourceTreeOrdinal,
                    effectEvent.SourceTreePath,
                    effectEvent.SourceTreeSha256,
                    effectEvent.SourceLineMapSha256,
                    compilation) ||
                effectEvent.Location.Start != effectEvent.SyntaxStart ||
                effectEvent.Location.Length != effectEvent.SyntaxLength)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidOutcome(CompilerEffectClaimArtifact value)
    {
        return CompilerEffectEvidenceCatalog.HasValidEffectTuple(
            value.Outcome, value.Reason, value.Certainty) &&
        (value.Outcome, value.Reason, value.Certainty, value.Witness, value.Replay) switch
        {
            (WorkerClaimOutcome.Proven, WorkerClaimReason.None, _, null, null) => true,
            (WorkerClaimOutcome.Refuted, WorkerClaimReason.None,
                _, { } witness, { }) => WorkerProtocolJson.HasValidEffectWitness(witness) &&
                    HasCanonicalStrings(witness.ExactExceptionTypeHierarchy) &&
                    WorkerProtocolJson.HasValidLocation(witness.Location),
            (WorkerClaimOutcome.Unknown,
                var reason, _, null, null) when
                CompilerEffectEvidenceCatalog.UnknownReasons.Contains(reason) => true,
            _ => false
        };
    }

    private static bool HasValidConstraint(
        WorkerEffectContractKind kind,
        CompilerEffectConstraintArtifact? constraint)
    {
        if (constraint is not { } value ||
            !WorkerProtocolJson.HasKnownEffects(
                value.AllowedEffects, value.AllowedCapabilities) ||
            !HasCanonicalStrings(value.AllowedExceptionTypes))
        {
            return false;
        }

        var rule = CompilerEffectEvidenceCatalog.ConstraintRules
            .FirstOrDefault(candidate => candidate.Kind == kind);
        return rule.Kind == kind &&
            (!rule.EffectsMustBeEmpty || value.AllowedEffects == WorkerEffectSet.None) &&
            (!rule.CapabilitiesMustBeEmpty || value.AllowedCapabilities == WorkerEffectCapabilitySet.None) &&
            (!rule.ExceptionsMustBeEmpty || value.AllowedExceptionTypes.Length == 0);
    }

    private static bool TryValidateAndComputeEvidenceSha256(
        CompilerEffectClaimArtifact value,
        out string? evidenceSha256)
    {
        evidenceSha256 = null;
        var replay = value.Replay;
        if (replay == null)
        {
            if (value.Outcome == WorkerClaimOutcome.Refuted)
            {
                return false;
            }

            using var emptyReplayHash = StartEvidenceHash(value);
            evidenceSha256 = FinishEvidenceHash(emptyReplayHash, value);
            return true;
        }

        if (value.Outcome != WorkerClaimOutcome.Refuted ||
            replay.PathKind != CompilerEffectEvidenceCatalog.ReplayPathKind ||
            replay.Events is not { Length: > 0 and <= CompilerEffectEvidenceCatalog.MaximumReplayEvents } ||
            replay.ConstraintSha256 != ComputeConstraintSha256(value.ContractKind, value.Constraint))
        {
            return false;
        }

        using var hash = StartEvidenceHash(value);
        for (var index = 0; index < replay.Events.Length; index++)
        {
            var effectEvent = replay.Events[index];
            using var operationHash = new CanonicalHashWriter();
            operationHash.Add(CompilerEffectEvidenceCatalog.OperationDomain)
                .Add(CompilerEffectEvidenceCatalog.OperationVersion);
            if (!TryAddValidatedReplayEvent(
                    hash,
                    operationHash,
                    effectEvent,
                    index))
            {
                return false;
            }

            if (!StringComparer.Ordinal.Equals(
                    effectEvent.OperationIdentitySha256,
                    operationHash.Finish()))
            {
                return false;
            }
        }

        evidenceSha256 = FinishEvidenceHash(hash, value);
        return true;
    }

    private static bool TryAddValidatedReplayEvent(
        CanonicalHashWriter evidenceHash,
        CanonicalHashWriter operationHash,
        CompilerEffectReplayEventArtifact? value,
        int ordinal)
    {
        if (value == null || value.Ordinal != ordinal ||
            !CompilerEffectEvidenceCatalog.SupportedReplayEventKinds.Contains(value.Kind) ||
            value.SyntaxTreeOrdinal < 0 ||
            !WorkerProtocolJson.IsSha256(value.SyntaxTreeSha256) ||
            !WorkerProtocolJson.IsSha256(value.SyntaxTreeSnapshotSha256) ||
            !WorkerProtocolJson.IsSha256(value.SyntaxTreeLineMapSha256) ||
            value.SourceTreeOrdinal < 0 ||
            value.SourceTreeOrdinal != value.SyntaxTreeOrdinal ||
            string.IsNullOrWhiteSpace(value.SourceTreePath) ||
            !WorkerProtocolJson.IsSha256(value.SourceTreeSha256) ||
            !WorkerProtocolJson.IsSha256(value.SourceLineMapSha256) ||
            value.SyntaxStart < 0 || value.SyntaxLength <= 0 ||
            value.SyntaxStart > int.MaxValue - value.SyntaxLength ||
            string.IsNullOrWhiteSpace(value.TypeIdentity) ||
            !HasOptionalText(value.MemberDocumentationId) ||
            !HasOptionalText(value.TypeDocumentationId) ||
            value.ScalarOperands is not { Length: 0 } ||
            value.ExactExceptionTypeHierarchy is not { } ||
            !WorkerProtocolJson.HasValidLocation(value.Location) ||
            value.Location.Start != value.SyntaxStart ||
            value.Location.Length != value.SyntaxLength)
        {
            return false;
        }

        var exceptionTypes = value.ExactExceptionTypeHierarchy;
        var validShape = value.Kind switch
        {
            CompilerEffectReplayEventKind.ManagedObjectAllocation or
            CompilerEffectReplayEventKind.MonitorCall =>
                !string.IsNullOrWhiteSpace(value.MemberIdentity) &&
                exceptionTypes.Length == 0,
            CompilerEffectReplayEventKind.ManagedArrayAllocation or
            CompilerEffectReplayEventKind.EmptyLock =>
                string.IsNullOrEmpty(value.MemberIdentity) &&
                value.MemberDocumentationId == null &&
                exceptionTypes.Length == 0,
            CompilerEffectReplayEventKind.ExplicitThrow =>
                !string.IsNullOrWhiteSpace(value.MemberIdentity) &&
                exceptionTypes.Length > 0,
            _ => false
        };
        if (!validShape)
        {
            return false;
        }

        var (canonicalExceptions, containsType) =
            AddReplayEventPair(evidenceHash, operationHash, value);
        return value.Kind switch
        {
            CompilerEffectReplayEventKind.ManagedObjectAllocation or
            CompilerEffectReplayEventKind.MonitorCall =>
                true,
            CompilerEffectReplayEventKind.ManagedArrayAllocation or
            CompilerEffectReplayEventKind.EmptyLock =>
                true,
            CompilerEffectReplayEventKind.ExplicitThrow =>
                canonicalExceptions && containsType,
            _ => false
        };
    }

    private static bool HasOptionalText(string? value)
    {
        return value == null || !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasCanonicalStrings(string[]? values)
    {
        if (values == null)
        {
            return false;
        }

        string? previous = null;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                previous != null &&
                StringComparer.Ordinal.Compare(previous, value) >= 0)
            {
                return false;
            }

            previous = value;
        }

        return true;
    }

    internal static string ComputeConstraintSha256(
        WorkerEffectContractKind kind,
        CompilerEffectConstraintArtifact constraint)
    {
        constraint = ArgumentNullGuard.NotNull(constraint, nameof(constraint));

        using var hash = new CanonicalHashWriter();
        hash.Add(CompilerEffectEvidenceCatalog.ConstraintDomain)
            .Add(CompilerEffectEvidenceCatalog.ConstraintVersion)
            .Add(kind)
            .Add(constraint.AllowedEffects)
            .Add(constraint.AllowedCapabilities);
        AddSortedStrings(hash, constraint.AllowedExceptionTypes ?? []);

        return hash.Finish();
    }

    internal static string ComputeReplayOperationSha256(
        CompilerEffectReplayEventArtifact value)
    {
        value = ArgumentNullGuard.NotNull(value, nameof(value));

        using var hash = new CanonicalHashWriter();
        hash.Add(CompilerEffectEvidenceCatalog.OperationDomain)
            .Add(CompilerEffectEvidenceCatalog.OperationVersion);
        AddReplayEvent(hash, value, includeOrdinal: false, includeOperationIdentity: false);
        return hash.Finish();
    }

    private static CanonicalHashWriter StartEvidenceHash(
        CompilerEffectClaimArtifact value)
    {
        var witness = value.Witness;
        var constraint = value.Constraint;
        var hash = new CanonicalHashWriter();
        hash.Add(CompilerEffectEvidenceCatalog.EvidenceDomain)
            .Add(CompilerEffectEvidenceCatalog.EvidenceVersion)
            .Add(value.ClaimId)
            .Add(value.ContractKind)
            .Add(value.Outcome)
            .Add(value.Reason)
            .Add(value.Certainty)
            .Add(constraint.AllowedEffects)
            .Add(constraint.AllowedCapabilities);
        AddSortedStrings(hash, constraint.AllowedExceptionTypes);

        hash.Add(witness?.Kind)
            .Add(witness?.Detail)
            .Add(witness?.Effects ?? WorkerEffectSet.None)
            .Add(witness?.Capabilities ?? WorkerEffectCapabilitySet.None);
        AddSortedStrings(hash, witness?.ExactExceptionTypeHierarchy ?? []);

        var replay = value.Replay;
        hash.Add(replay != null)
            .Add(replay?.PathKind ?? CompilerEffectReplayPathKind.Unspecified)
            .Add(replay?.ConstraintSha256)
            .Add(replay?.Events?.Length ?? -1);
        return hash;
    }

    private static string FinishEvidenceHash(
        CanonicalHashWriter hash,
        CompilerEffectClaimArtifact value)
    {
        var witness = value.Witness;
        return hash.Add(witness?.Location.Path)
            .Add(witness?.Location.Start ?? -1)
            .Add(witness?.Location.Length ?? -1)
            .Add(witness?.Location.Line ?? -1)
            .Add(witness?.Location.Column ?? -1)
            .Add(value.Evidence)
            .Finish();
    }

    private static string ComputeSha256(CompilerEffectClaimArtifact value)
    {
        using var hash = StartEvidenceHash(value);
        foreach (var effectEvent in value.Replay?.Events ?? [])
        {
            AddReplayEvent(
                hash,
                effectEvent,
                includeOrdinal: true,
                includeOperationIdentity: true);
        }
        return FinishEvidenceHash(hash, value);
    }

    private static void AddSortedStrings(
        CanonicalHashWriter hash,
        string[] values)
    {
        foreach (var value in values.OrderBy(static item => item, StringComparer.Ordinal))
        {
            hash.Add(value);
        }
    }

    private static void AddReplayEvent(
        CanonicalHashWriter hash,
        CompilerEffectReplayEventArtifact value,
        bool includeOrdinal,
        bool includeOperationIdentity)
    {
        if (includeOrdinal)
        {
            hash.Add(value.Ordinal);
        }

        hash.Add(value.Kind)
            .Add(value.SyntaxTreeOrdinal)
            .Add(value.SyntaxTreeSha256)
            .Add(value.SyntaxTreeSnapshotSha256)
            .Add(value.SyntaxTreeLineMapSha256)
            .Add(value.SyntaxStart)
            .Add(value.SyntaxLength);
        if (includeOperationIdentity)
        {
            hash.Add(value.OperationIdentitySha256);
        }

        // Array-allocation events canonically have no member identity. Treat
        // the wire-level null and empty representations as the same value so
        // replay semantics and operation hashes cannot diverge.
        hash.Add(value.MemberIdentity ?? string.Empty)
            .Add(value.MemberDocumentationId)
            .Add(value.TypeIdentity)
            .Add(value.TypeDocumentationId)
            .Add(value.SpecWitnessIdentifier);
        hash.Add(value.SourceTreeOrdinal)
            .Add(value.SourceTreePath)
            .Add(value.SourceTreeSha256)
            .Add(value.SourceLineMapSha256);
        var operands = value.ScalarOperands ?? [];
        hash.Add(operands.Length);
        foreach (var operand in operands)
        {
            hash.Add(operand);
        }

        var exceptionTypes = value.ExactExceptionTypeHierarchy ?? [];
        hash.Add(exceptionTypes.Length);
        foreach (var type in exceptionTypes)
        {
            hash.Add(type);
        }

        var location = value.Location;
        hash.Add(location?.Path)
            .Add(location?.Start ?? -1)
            .Add(location?.Length ?? -1)
            .Add(location?.Line ?? -1)
            .Add(location?.Column ?? -1);
    }

    private static (bool CanonicalExceptions, bool ContainsType)
        AddReplayEventPair(
        CanonicalHashWriter evidenceHash,
        CanonicalHashWriter operationHash,
        CompilerEffectReplayEventArtifact value)
    {
        evidenceHash.Add(value.Ordinal);
        evidenceHash.Add(value.Kind);
        operationHash.Add(value.Kind);
        evidenceHash.Add(value.SyntaxTreeOrdinal);
        operationHash.Add(value.SyntaxTreeOrdinal);
        evidenceHash.Add(value.SyntaxTreeSha256);
        operationHash.Add(value.SyntaxTreeSha256);
        evidenceHash.Add(value.SyntaxTreeSnapshotSha256);
        operationHash.Add(value.SyntaxTreeSnapshotSha256);
        evidenceHash.Add(value.SyntaxTreeLineMapSha256);
        operationHash.Add(value.SyntaxTreeLineMapSha256);
        evidenceHash.Add(value.SyntaxStart);
        operationHash.Add(value.SyntaxStart);
        evidenceHash.Add(value.SyntaxLength);
        operationHash.Add(value.SyntaxLength);
        evidenceHash.Add(value.OperationIdentitySha256);

        var memberIdentity = value.MemberIdentity ?? string.Empty;
        evidenceHash.Add(memberIdentity);
        operationHash.Add(memberIdentity);
        evidenceHash.Add(value.MemberDocumentationId);
        operationHash.Add(value.MemberDocumentationId);
        evidenceHash.Add(value.TypeIdentity);
        operationHash.Add(value.TypeIdentity);
        evidenceHash.Add(value.TypeDocumentationId);
        operationHash.Add(value.TypeDocumentationId);
        evidenceHash.Add(value.SpecWitnessIdentifier);
        operationHash.Add(value.SpecWitnessIdentifier);
        evidenceHash.Add(value.SourceTreeOrdinal);
        operationHash.Add(value.SourceTreeOrdinal);
        evidenceHash.Add(value.SourceTreePath);
        operationHash.Add(value.SourceTreePath);
        evidenceHash.Add(value.SourceTreeSha256);
        operationHash.Add(value.SourceTreeSha256);
        evidenceHash.Add(value.SourceLineMapSha256);
        operationHash.Add(value.SourceLineMapSha256);

        var operands = value.ScalarOperands ?? [];
        evidenceHash.Add(operands.Length);
        operationHash.Add(operands.Length);
        foreach (var operand in operands)
        {
            evidenceHash.Add(operand);
            operationHash.Add(operand);
        }

        var exceptionTypes = value.ExactExceptionTypeHierarchy ?? [];
        evidenceHash.Add(exceptionTypes.Length);
        operationHash.Add(exceptionTypes.Length);
        var canonicalExceptions = true;
        var containsType = false;
        string? previousExceptionType = null;
        foreach (var type in exceptionTypes)
        {
            evidenceHash.Add(type);
            operationHash.Add(type);
            if (string.IsNullOrWhiteSpace(type) ||
                previousExceptionType != null &&
                StringComparer.Ordinal.Compare(
                    previousExceptionType,
                    type) >= 0)
            {
                canonicalExceptions = false;
            }
            containsType |= StringComparer.Ordinal.Equals(
                type,
                value.TypeIdentity);
            previousExceptionType = type;
        }

        var location = value.Location;
        evidenceHash.Add(location?.Path);
        operationHash.Add(location?.Path);
        evidenceHash.Add(location?.Start ?? -1);
        operationHash.Add(location?.Start ?? -1);
        evidenceHash.Add(location?.Length ?? -1);
        operationHash.Add(location?.Length ?? -1);
        evidenceHash.Add(location?.Line ?? -1);
        operationHash.Add(location?.Line ?? -1);
        evidenceHash.Add(location?.Column ?? -1);
        operationHash.Add(location?.Column ?? -1);
        return (canonicalExceptions, containsType);
    }
}
