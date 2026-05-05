using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace PurelySharp.Tools.CorpusReport;

public sealed record CorpusReportSummary(
    ImmutableArray<string> Inputs,
    int Ps0002Count,
    int Ps0004Count,
    int Ps0009Count,
    int TotalPurelySharpDiagnostics,
    ImmutableArray<DiagnosticEvidenceItem> Diagnostics,
    ImmutableDictionary<string, int> ImpurityCategories,
    ImmutableDictionary<string, int> RuleNames,
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
        ImmutableArray<DiagnosticEvidenceItem>.Empty,
        ImmutableDictionary<string, int>.Empty,
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

public sealed record DiagnosticEvidenceItem(
    string Input,
    string RuleId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RuleName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OperationKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Symbol,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CatalogSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CalleeChain);
