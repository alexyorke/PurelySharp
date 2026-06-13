using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer
{
    internal sealed class ExceptionSummaryCatalog
    {
        private const string SummaryFileName = "PurelySharp.EffectSummary.json";
        private static readonly Regex SymbolRegex = new Regex(@"""Symbol""\s*:\s*""(?<value>(?:\\.|[^""])*)""", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ExceptionArrayRegex = new Regex(@"""(?<name>ThrownExceptionTypes|TransitiveThrownExceptionTypes)""\s*:\s*\[(?<values>.*?)\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex JsonStringRegex = new Regex(@"""(?<value>(?:\\.|[^""])*)""", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        public static readonly ExceptionSummaryCatalog Empty = new ExceptionSummaryCatalog(
            ImmutableDictionary<string, ImmutableArray<string>>.Empty);

        private readonly ImmutableDictionary<string, ImmutableArray<string>> _exceptionsBySymbol;

        private ExceptionSummaryCatalog(ImmutableDictionary<string, ImmutableArray<string>> exceptionsBySymbol)
        {
            _exceptionsBySymbol = exceptionsBySymbol;
        }

        public static ExceptionSummaryCatalog FromOptions(AnalyzerOptions options, CancellationToken cancellationToken)
        {
            var summaries = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (var additionalFile in options.AdditionalFiles)
            {
                if (!IsSummaryFile(additionalFile.Path))
                {
                    continue;
                }

                var text = additionalFile.GetText(cancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                foreach (var entry in ParseEntries(text!))
                {
                    if (!summaries.TryGetValue(entry.Symbol, out var exceptions))
                    {
                        exceptions = new SortedSet<string>(StringComparer.Ordinal);
                        summaries.Add(entry.Symbol, exceptions);
                    }

                    foreach (var exceptionType in entry.ExceptionTypes)
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }

            if (summaries.Count == 0)
            {
                return Empty;
            }

            return new ExceptionSummaryCatalog(summaries.ToImmutableDictionary(
                item => item.Key,
                item => item.Value.ToImmutableArray(),
                StringComparer.Ordinal));
        }

        public bool TryGetExceptions(IMethodSymbol methodSymbol, out ImmutableArray<string> exceptionTypes)
        {
            foreach (var key in GetSymbolKeys(methodSymbol))
            {
                if (_exceptionsBySymbol.TryGetValue(key, out exceptionTypes) &&
                    !exceptionTypes.IsDefaultOrEmpty)
                {
                    return true;
                }
            }

            exceptionTypes = ImmutableArray<string>.Empty;
            return false;
        }

        private static bool IsSummaryFile(string path)
        {
            var fileName = System.IO.Path.GetFileName(path);
            return string.Equals(fileName, SummaryFileName, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("." + SummaryFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<SummaryEntry> ParseEntries(string json)
        {
            foreach (var objectBody in EnumerateObjectBodies(json))
            {
                if (ContainsUnquotedBrace(objectBody))
                {
                    continue;
                }

                var symbolMatch = SymbolRegex.Match(objectBody);
                if (!symbolMatch.Success)
                {
                    continue;
                }

                var exceptionTypes = new SortedSet<string>(StringComparer.Ordinal);
                foreach (Match arrayMatch in ExceptionArrayRegex.Matches(objectBody))
                {
                    foreach (Match stringMatch in JsonStringRegex.Matches(arrayMatch.Groups["values"].Value))
                    {
                        var value = UnescapeJsonString(stringMatch.Groups["value"].Value).Trim();
                        if (value.Length > 0)
                        {
                            exceptionTypes.Add(value);
                        }
                    }
                }

                if (exceptionTypes.Count == 0)
                {
                    continue;
                }

                yield return new SummaryEntry(
                    UnescapeJsonString(symbolMatch.Groups["value"].Value),
                    exceptionTypes.ToImmutableArray());
            }
        }

        private static IEnumerable<string> GetSymbolKeys(IMethodSymbol methodSymbol)
        {
            var keys = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            AddSymbolKey(keys, methodSymbol.OriginalDefinition.ToDisplayString());
            AddSymbolKey(keys, methodSymbol.ToDisplayString());
            AddSymbolKey(keys, CreateEffectSummaryKey(methodSymbol));

            if (methodSymbol.IsGenericMethod)
            {
                AddSymbolKey(keys, methodSymbol.ConstructedFrom.ToDisplayString());
                AddSymbolKey(keys, CreateEffectSummaryKey(methodSymbol.ConstructedFrom));
            }

            foreach (var key in keys)
            {
                yield return key;
            }
        }

        private static void AddSymbolKey(ImmutableHashSet<string>.Builder keys, string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key!);
            }
        }

        private static string? CreateEffectSummaryKey(IMethodSymbol methodSymbol)
        {
            var containingType = methodSymbol.ContainingType?.OriginalDefinition.ToDisplayString();
            if (string.IsNullOrWhiteSpace(containingType))
            {
                return null;
            }

            var methodName = methodSymbol.MethodKind == MethodKind.Constructor
                ? ".ctor"
                : methodSymbol.MetadataName;
            var parameters = string.Join(", ", methodSymbol.Parameters.Select(CreateEffectParameterName));
            return $"{containingType}.{methodName}({parameters})";
        }

        private static string CreateEffectParameterName(IParameterSymbol parameter)
        {
            var typeName = CreateEffectTypeName(parameter.Type);
            return parameter.RefKind == RefKind.None ? typeName : "ref " + typeName;
        }

        private static string CreateEffectTypeName(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is IArrayTypeSymbol arrayType)
            {
                var rank = Math.Max(arrayType.Rank, 1);
                return $"{CreateEffectTypeName(arrayType.ElementType)}[{new string(',', rank - 1)}]";
            }

            if (typeSymbol is ITypeParameterSymbol typeParameter)
            {
                return typeParameter.TypeParameterKind == TypeParameterKind.Method
                    ? "!!" + typeParameter.Ordinal
                    : "!" + typeParameter.Ordinal;
            }

            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return "bool";
                case SpecialType.System_Byte:
                    return "byte";
                case SpecialType.System_Char:
                    return "char";
                case SpecialType.System_Double:
                    return "double";
                case SpecialType.System_Int16:
                    return "short";
                case SpecialType.System_Int32:
                    return "int";
                case SpecialType.System_Int64:
                    return "long";
                case SpecialType.System_IntPtr:
                    return "nint";
                case SpecialType.System_Object:
                    return "object";
                case SpecialType.System_SByte:
                    return "sbyte";
                case SpecialType.System_Single:
                    return "float";
                case SpecialType.System_String:
                    return "string";
                case SpecialType.System_UInt16:
                    return "ushort";
                case SpecialType.System_UInt32:
                    return "uint";
                case SpecialType.System_UInt64:
                    return "ulong";
                case SpecialType.System_UIntPtr:
                    return "nuint";
                case SpecialType.System_Void:
                    return "void";
            }

            if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var typeName = namedType.OriginalDefinition.ToDisplayString();
                var tickIndex = typeName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    typeName = typeName.Substring(0, tickIndex);
                }

                return $"{typeName}<{string.Join(", ", namedType.TypeArguments.Select(CreateEffectTypeName))}>";
            }

            return typeSymbol.OriginalDefinition.ToDisplayString();
        }

        private static IEnumerable<string> EnumerateObjectBodies(string json)
        {
            var objectStarts = new Stack<int>();
            var inString = false;
            var escaped = false;

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                }
                else if (ch == '{')
                {
                    objectStarts.Push(i + 1);
                }
                else if (ch == '}' && objectStarts.Count > 0)
                {
                    var start = objectStarts.Pop();
                    yield return json.Substring(start, i - start);
                }
            }
        }

        private static bool ContainsUnquotedBrace(string value)
        {
            var inString = false;
            var escaped = false;

            foreach (var ch in value)
            {
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                }
                else if (ch == '{' || ch == '}')
                {
                    return true;
                }
            }

            return false;
        }

        private static string UnescapeJsonString(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(ch);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '/':
                        builder.Append('/');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 < value.Length &&
                            int.TryParse(
                                value.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var codePoint))
                        {
                            builder.Append((char)codePoint);
                            i += 4;
                        }
                        break;
                    default:
                        builder.Append(value[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        private sealed class SummaryEntry
        {
            public SummaryEntry(string symbol, ImmutableArray<string> exceptionTypes)
            {
                Symbol = symbol;
                ExceptionTypes = exceptionTypes;
            }

            public string Symbol { get; }

            public ImmutableArray<string> ExceptionTypes { get; }
        }
    }
}
