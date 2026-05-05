using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PurelySharp.Tools.CorpusReport;

public sealed record CorpusReportSummary(
    ImmutableArray<string> Inputs,
    int Ps0002Count,
    int Ps0004Count,
    int Ps0009Count,
    int TotalPurelySharpDiagnostics,
    ImmutableDictionary<string, int> ImpurityCategories,
    ImmutableDictionary<string, int> OperationKinds,
    ImmutableDictionary<string, int> UnknownOperationKinds,
    ImmutableArray<RankedItem> TopImpureApis,
    ImmutableArray<RankedItem> CatalogMisses,
    ImmutableArray<RankedItem> FalsePositiveCandidates)
{
    public static CorpusReportSummary Empty { get; } = new(
        ImmutableArray<string>.Empty,
        0,
        0,
        0,
        0,
        ImmutableDictionary<string, int>.Empty,
        ImmutableDictionary<string, int>.Empty,
        ImmutableDictionary<string, int>.Empty,
        ImmutableArray<RankedItem>.Empty,
        ImmutableArray<RankedItem>.Empty,
        ImmutableArray<RankedItem>.Empty);
}

public sealed record RankedItem(
    string Value,
    int Count,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Category = null);
