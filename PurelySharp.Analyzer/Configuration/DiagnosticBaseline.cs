using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Configuration
{
    internal sealed class DiagnosticBaseline
    {
        private const string BaselineFileName = "PurelySharp.Baseline.json";
        private static readonly Regex ObjectRegex = new Regex(@"\{(?<body>[^{}]*)\}", RegexOptions.Singleline);
        private static readonly Regex PropertyRegex = new Regex(@"""(?<name>id|diagnosticId|symbol|path)""\s*:\s*""(?<value>(?:\\.|[^""])*)""", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public static readonly DiagnosticBaseline Empty = new DiagnosticBaseline(ImmutableArray<BaselineEntry>.Empty);

        private readonly ImmutableArray<BaselineEntry> _entries;

        private DiagnosticBaseline(ImmutableArray<BaselineEntry> entries)
        {
            _entries = entries;
        }

        public static DiagnosticBaseline FromOptions(AnalyzerOptions options, CancellationToken cancellationToken)
        {
            var builder = ImmutableArray.CreateBuilder<BaselineEntry>();
            foreach (var additionalFile in options.AdditionalFiles)
            {
                if (!string.Equals(System.IO.Path.GetFileName(additionalFile.Path), BaselineFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = additionalFile.GetText(cancellationToken)?.ToString();
                if (text == null || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (var entry in ParseEntries(text))
                {
                    builder.Add(entry);
                }
            }

            return builder.Count == 0 ? Empty : new DiagnosticBaseline(builder.ToImmutable());
        }

        public bool IsSuppressed(string diagnosticId, ISymbol symbol, SyntaxTree syntaxTree)
        {
            if (_entries.IsDefaultOrEmpty)
            {
                return false;
            }

            var symbolIds = GetSymbolIds(symbol);
            var sourcePath = syntaxTree.FilePath ?? string.Empty;

            foreach (var entry in _entries)
            {
                foreach (var symbolId in symbolIds)
                {
                    if (entry.Matches(diagnosticId, symbolId, sourcePath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ImmutableArray<string> GetSymbolIds(ISymbol symbol)
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            var documentationId = DocumentationCommentId.CreateDeclarationId(symbol.OriginalDefinition);
            if (!string.IsNullOrWhiteSpace(documentationId))
            {
                builder.Add(documentationId!);
            }

            builder.Add(symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));

            if (symbol is IMethodSymbol methodSymbol && methodSymbol.ContainingType != null)
            {
                var containingType = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                var methodName = methodSymbol.MetadataName == ".ctor" ? "#ctor" : methodSymbol.MetadataName;
                builder.Add("M:" + containingType + "." + methodName);
            }

            return builder.Distinct(StringComparer.Ordinal).ToImmutableArray();
        }

        private static ImmutableArray<BaselineEntry> ParseEntries(string json)
        {
            var builder = ImmutableArray.CreateBuilder<BaselineEntry>();
            foreach (Match objectMatch in ObjectRegex.Matches(json))
            {
                string? id = null;
                string? symbol = null;
                string? path = null;

                foreach (Match propertyMatch in PropertyRegex.Matches(objectMatch.Groups["body"].Value))
                {
                    var name = propertyMatch.Groups["name"].Value;
                    var value = UnescapeJsonString(propertyMatch.Groups["value"].Value);
                    if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "diagnosticId", StringComparison.OrdinalIgnoreCase))
                    {
                        id = value;
                    }
                    else if (string.Equals(name, "symbol", StringComparison.OrdinalIgnoreCase))
                    {
                        symbol = value;
                    }
                    else if (string.Equals(name, "path", StringComparison.OrdinalIgnoreCase))
                    {
                        path = value;
                    }
                }

                if (!string.IsNullOrWhiteSpace(id) &&
                    !string.IsNullOrWhiteSpace(symbol) &&
                    !string.IsNullOrWhiteSpace(path))
                {
                    builder.Add(new BaselineEntry(id!, symbol!, path!));
                }
            }

            return builder.ToImmutable();
        }

        private static string UnescapeJsonString(string value)
        {
            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private readonly struct BaselineEntry
        {
            public BaselineEntry(string diagnosticId, string symbolId, string path)
            {
                DiagnosticId = diagnosticId;
                SymbolId = symbolId;
                Path = NormalizePath(path);
            }

            private string DiagnosticId { get; }
            private string SymbolId { get; }
            private string Path { get; }

            public bool Matches(string diagnosticId, string symbolId, string sourcePath)
            {
                return string.Equals(DiagnosticId, diagnosticId, StringComparison.Ordinal) &&
                       string.Equals(SymbolId, symbolId, StringComparison.Ordinal) &&
                       MatchesPath(sourcePath);
            }

            private bool MatchesPath(string sourcePath)
            {
                var normalizedSourcePath = NormalizePath(sourcePath);
                return string.Equals(Path, normalizedSourcePath, StringComparison.OrdinalIgnoreCase) ||
                       normalizedSourcePath.EndsWith("/" + Path, StringComparison.OrdinalIgnoreCase);
            }

            private static string NormalizePath(string path)
            {
                var normalized = path.Replace('\\', '/').Trim();
                while (normalized.StartsWith("./", StringComparison.Ordinal))
                {
                    normalized = normalized.Substring(2);
                }

                return normalized;
            }
        }
    }
}
