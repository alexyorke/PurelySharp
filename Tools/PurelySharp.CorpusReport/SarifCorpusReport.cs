using System.Collections.Immutable;
using System.Text.Json;

namespace PurelySharp.Tools.CorpusReport;

public static class SarifCorpusReport
{
    private const string CategoryProperty = "purelysharp.impurity.category";
    private const string OperationKindProperty = "purelysharp.impurity.operation_kind";
    private const string SymbolProperty = "purelysharp.impurity.symbol";

    private static readonly ImmutableHashSet<string> CatalogMissCategories =
        ImmutableHashSet.Create(StringComparer.Ordinal, "unknown_external_call", "unsupported_operation");

    private static readonly ImmutableHashSet<string> FalsePositiveCandidateCategories =
        ImmutableHashSet.Create(StringComparer.Ordinal, "unknown_external_call", "dynamic_dispatch", "unsupported_operation", "unresolved_delegate_target");

    public static CorpusReportSummary CreateFromSarifFiles(IEnumerable<string> sarifPaths)
    {
        var builder = new SummaryBuilder();
        foreach (var sarifPath in sarifPaths)
        {
            builder.AddSarifJson(sarifPath, File.ReadAllText(sarifPath));
        }

        return builder.Build();
    }

    public static CorpusReportSummary CreateFromSarifJson(string inputName, string sarifJson)
    {
        var builder = new SummaryBuilder();
        builder.AddSarifJson(inputName, sarifJson);
        return builder.Build();
    }

    private sealed class SummaryBuilder
    {
        private readonly ImmutableArray<string>.Builder _inputs = ImmutableArray.CreateBuilder<string>();
        private readonly Dictionary<string, int> _categories = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _operationKinds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _unknownOperationKinds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _symbols = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _catalogMisses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Category, int Count)> _falsePositiveCandidates = new(StringComparer.Ordinal);

        private int _ps0002Count;
        private int _ps0004Count;
        private int _ps0009Count;
        private int _totalPurelySharpDiagnostics;

        public void AddSarifJson(string inputName, string sarifJson)
        {
            _inputs.Add(inputName);
            using var document = JsonDocument.Parse(sarifJson);
            if (!document.RootElement.TryGetProperty("runs", out var runs) ||
                runs.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var run in runs.EnumerateArray())
            {
                if (!run.TryGetProperty("results", out var results) ||
                    results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in results.EnumerateArray())
                {
                    AddResult(result);
                }
            }
        }

        public CorpusReportSummary Build()
        {
            return new CorpusReportSummary(
                _inputs.ToImmutable(),
                _ps0002Count,
                _ps0004Count,
                _ps0009Count,
                _totalPurelySharpDiagnostics,
                ToImmutableSortedDictionary(_categories),
                ToImmutableSortedDictionary(_operationKinds),
                ToImmutableSortedDictionary(_unknownOperationKinds),
                ToRankedItems(_symbols),
                ToRankedItems(_catalogMisses),
                ToFalsePositiveRankedItems(_falsePositiveCandidates));
        }

        private void AddResult(JsonElement result)
        {
            var ruleId = GetStringProperty(result, "ruleId");
            if (ruleId is null || !ruleId.StartsWith("PS", StringComparison.Ordinal))
            {
                return;
            }

            _totalPurelySharpDiagnostics++;
            if (ruleId == "PS0002")
            {
                _ps0002Count++;
            }
            else if (ruleId == "PS0004")
            {
                _ps0004Count++;
            }
            else if (ruleId == "PS0009")
            {
                _ps0009Count++;
            }

            if (!result.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var category = GetStringProperty(properties, CategoryProperty);
            var operationKind = GetStringProperty(properties, OperationKindProperty);
            var symbol = GetStringProperty(properties, SymbolProperty);

            IncrementIfPresent(_categories, category);
            IncrementIfPresent(_operationKinds, operationKind);
            IncrementIfPresent(_symbols, symbol);

            if (string.Equals(category, "unsupported_operation", StringComparison.Ordinal))
            {
                IncrementIfPresent(_unknownOperationKinds, operationKind);
            }

            if (category != null && symbol != null && CatalogMissCategories.Contains(category))
            {
                Increment(_catalogMisses, symbol);
            }

            if (category != null && symbol != null && FalsePositiveCandidateCategories.Contains(category))
            {
                var key = category + "|" + symbol;
                _falsePositiveCandidates[key] = _falsePositiveCandidates.TryGetValue(key, out var existing)
                    ? (category, existing.Count + 1)
                    : (category, 1);
            }
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static void IncrementIfPresent(Dictionary<string, int> values, string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Increment(values, key);
            }
        }

        private static void Increment(Dictionary<string, int> values, string key)
        {
            values[key] = values.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        private static ImmutableDictionary<string, int> ToImmutableSortedDictionary(Dictionary<string, int> values)
        {
            return values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToImmutableDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        private static ImmutableArray<RankedItem> ToRankedItems(Dictionary<string, int> values)
        {
            return values
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new RankedItem(pair.Key, pair.Value))
                .ToImmutableArray();
        }

        private static ImmutableArray<RankedItem> ToFalsePositiveRankedItems(Dictionary<string, (string Category, int Count)> values)
        {
            return values
                .Select(pair =>
                {
                    var separatorIndex = pair.Key.IndexOf('|');
                    var symbol = separatorIndex >= 0 ? pair.Key[(separatorIndex + 1)..] : pair.Key;
                    return new RankedItem(symbol, pair.Value.Count, pair.Value.Category);
                })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Category, StringComparer.Ordinal)
                .ThenBy(item => item.Value, StringComparer.Ordinal)
                .ToImmutableArray();
        }
    }
}
