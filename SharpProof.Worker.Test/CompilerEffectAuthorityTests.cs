using System.Reflection;
using NUnit.Framework;
using SharpProof.CompilerArtifact;
using SharpProof.Worker.Protocol;

namespace SharpProof.Worker.Test;

[TestFixture]
public sealed class CompilerEffectAuthorityTests
{
    private static readonly Func<CompilerEffectReplayArtifact?,
        CompilerEffectReplayArtifact?, bool> s_replaysEqual =
        (Func<CompilerEffectReplayArtifact?, CompilerEffectReplayArtifact?, bool>)
        typeof(CompilerEffectAuthority)
            .GetMethod(
                "ReplaysEqual",
                BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate(
                typeof(Func<CompilerEffectReplayArtifact?,
                    CompilerEffectReplayArtifact?, bool>));

    private static readonly Func<CompilerEffectReplayEventArtifact?,
        CompilerEffectReplayEventArtifact?, bool> s_replayEventsEqual =
        (Func<CompilerEffectReplayEventArtifact?,
            CompilerEffectReplayEventArtifact?, bool>)
        typeof(CompilerEffectAuthority)
            .GetMethod(
                "ReplayEventsEqual",
                BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate(
                typeof(Func<CompilerEffectReplayEventArtifact?,
                    CompilerEffectReplayEventArtifact?, bool>));

    [Test]
    public void ReplaysEqualPreservesOrderAndHandlesNullReplaysAndEntries()
    {
        var first = CreateReplay(CreateEvent(0), CreateEvent(1));
        var same = CreateReplay(CreateEvent(0), CreateEvent(1));
        var reordered = CreateReplay(CreateEvent(1), CreateEvent(0));
        var nullEntry = CreateReplay(
            new CompilerEffectReplayEventArtifact[] { null! });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(s_replaysEqual(first, same), Is.True);
            Assert.That(s_replaysEqual(first, reordered), Is.False);
            Assert.That(
                s_replaysEqual(first, CreateReplay(CreateEvent(0))),
                Is.False);
            Assert.That(s_replaysEqual(CreateReplay(), CreateReplay()), Is.True);
            Assert.That(s_replaysEqual(null, null), Is.True);
            Assert.That(s_replaysEqual(null, first), Is.False);
            Assert.That(s_replaysEqual(first, null), Is.False);
            Assert.That(
                s_replaysEqual(
                    nullEntry,
                    CreateReplay(
                        new CompilerEffectReplayEventArtifact[] { null! })),
                Is.True);
            Assert.That(
                s_replaysEqual(nullEntry, CreateReplay(CreateEvent(0))),
                Is.False);
        }
    }

    [Test]
    public void ReplaysEqualRejectsNullEventArrays()
    {
        var malformed = CreateReplay(CreateEvent(0));
        malformed.Events = null!;
        var valid = CreateReplay(CreateEvent(0));

        Assert.Throws<ArgumentNullException>(
            (Action)(() => s_replaysEqual(malformed, valid)));
        Assert.Throws<ArgumentNullException>(
            (Action)(() => s_replaysEqual(valid, malformed)));
    }

    [Test]
    public void ReplayEventsEqualDiscriminatesEveryComparedProperty()
    {
        var baseline = CreateEvent(7);
        var mutations = new (string Name,
            Action<CompilerEffectReplayEventArtifact> Mutate)[]
        {
            ("Ordinal", value => value.Ordinal++),
            ("Kind", value => value.Kind =
                CompilerEffectReplayEventKind.EmptyLock),
            ("SyntaxTreeOrdinal", value => value.SyntaxTreeOrdinal++),
            ("SyntaxTreeSha256", value => value.SyntaxTreeSha256 += "-changed"),
            ("SyntaxTreeSnapshotSha256", value =>
                value.SyntaxTreeSnapshotSha256 += "-changed"),
            ("SyntaxTreeLineMapSha256", value =>
                value.SyntaxTreeLineMapSha256 += "-changed"),
            ("SyntaxStart", value => value.SyntaxStart++),
            ("SyntaxLength", value => value.SyntaxLength++),
            ("OperationIdentitySha256", value =>
                value.OperationIdentitySha256 += "-changed"),
            ("MemberIdentity", value => value.MemberIdentity += "-changed"),
            ("MemberDocumentationId", value =>
                value.MemberDocumentationId = "M:Changed"),
            ("TypeIdentity", value => value.TypeIdentity += "-changed"),
            ("TypeDocumentationId", value =>
                value.TypeDocumentationId = "T:Changed"),
            ("SpecWitnessIdentifier", value =>
                value.SpecWitnessIdentifier = "changed"),
            ("ScalarOperands", value => value.ScalarOperands = [3, 2]),
            ("ExactExceptionTypeHierarchy", value =>
                value.ExactExceptionTypeHierarchy = ["Derived", "Base"]),
            ("SourceTreeOrdinal", value => value.SourceTreeOrdinal++),
            ("SourceTreePath", value => value.SourceTreePath += "-changed"),
            ("SourceTreeSha256", value =>
                value.SourceTreeSha256 += "-changed"),
            ("SourceLineMapSha256", value =>
                value.SourceLineMapSha256 += "-changed"),
            ("Location", value => value.Location = new WorkerSourceLocation
            {
                Path = value.Location.Path,
                Start = value.Location.Start + 1,
                Length = value.Location.Length,
                Line = value.Location.Line,
                Column = value.Location.Column
            })
        };

        Assert.That(s_replayEventsEqual(baseline, CreateEvent(7)), Is.True);
        Assert.That(s_replayEventsEqual(null, null), Is.True);
        Assert.That(s_replayEventsEqual(null, baseline), Is.False);
        Assert.That(s_replayEventsEqual(baseline, null), Is.False);

        foreach (var (name, mutate) in mutations)
        {
            var changed = CreateEvent(7);
            mutate(changed);
            Assert.That(
                s_replayEventsEqual(baseline, changed),
                Is.False,
                name);
        }
    }

    private static CompilerEffectReplayArtifact CreateReplay(
        params CompilerEffectReplayEventArtifact[] events)
    {
        return new CompilerEffectReplayArtifact
        {
            PathKind = CompilerEffectReplayPathKind.Unconditional,
            ConstraintSha256 = "constraint",
            Events = events
        };
    }

    private static CompilerEffectReplayEventArtifact CreateEvent(int ordinal)
    {
        return new CompilerEffectReplayEventArtifact
        {
            Ordinal = ordinal,
            Kind = CompilerEffectReplayEventKind.MonitorCall,
            SyntaxTreeOrdinal = ordinal + 1,
            SyntaxTreeSha256 = "tree-" + ordinal,
            SyntaxTreeSnapshotSha256 = "snapshot-" + ordinal,
            SyntaxTreeLineMapSha256 = "line-map-" + ordinal,
            SyntaxStart = ordinal + 10,
            SyntaxLength = ordinal + 5,
            OperationIdentitySha256 = "operation-" + ordinal,
            MemberIdentity = "member-" + ordinal,
            MemberDocumentationId = "M:Subject" + ordinal,
            TypeIdentity = "type-" + ordinal,
            TypeDocumentationId = "T:Subject" + ordinal,
            SpecWitnessIdentifier = "spec-" + ordinal,
            ScalarOperands = [ordinal, ordinal + 1],
            ExactExceptionTypeHierarchy = ["Base" + ordinal, "Derived" + ordinal],
            Location = new WorkerSourceLocation
            {
                Path = "mapped-" + ordinal + ".cs",
                Start = ordinal + 10,
                Length = ordinal + 5,
                Line = ordinal + 2,
                Column = ordinal + 3
            },
            SourceTreeOrdinal = ordinal + 1,
            SourceTreePath = "source-" + ordinal + ".cs",
            SourceTreeSha256 = "source-hash-" + ordinal,
            SourceLineMapSha256 = "source-line-map-" + ordinal
        };
    }
}
