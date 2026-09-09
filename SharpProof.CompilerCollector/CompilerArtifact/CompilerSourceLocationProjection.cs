using Microsoft.CodeAnalysis;
using SharpProof.Worker.Protocol;

namespace SharpProof.CompilerArtifact;

internal static class CompilerSourceLocationProjection
{
    internal static WorkerSourceLocation Create(Location location)
    {
        if (!location.IsInSource)
        {
            return new WorkerSourceLocation();
        }

        var mapped = location.GetMappedLineSpan();
        return new WorkerSourceLocation
        {
            Path = mapped.Path,
            Start = location.SourceSpan.Start,
            Length = location.SourceSpan.Length,
            Line = mapped.StartLinePosition.Line + 1,
            Column = mapped.StartLinePosition.Character + 1
        };
    }
}
