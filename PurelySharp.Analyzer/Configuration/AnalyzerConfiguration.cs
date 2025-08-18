using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Configuration
{

    internal class AnalyzerConfiguration
    {
        public ImmutableHashSet<string> ExtraKnownImpureMethods { get; }
        public ImmutableHashSet<string> ExtraKnownPureMethods { get; }
        public ImmutableHashSet<string> ExtraKnownImpureNamespaces { get; }
        public ImmutableHashSet<string> ExtraKnownImpureTypes { get; }
        public bool EnableDebugLogging { get; }

        private AnalyzerConfiguration(
            ImmutableHashSet<string> extraImpureMethods,
            ImmutableHashSet<string> extraPureMethods,
            ImmutableHashSet<string> extraImpureNamespaces,
            ImmutableHashSet<string> extraImpureTypes,
            bool enableDebugLogging)
        {
            ExtraKnownImpureMethods = extraImpureMethods;
            ExtraKnownPureMethods = extraPureMethods;
            ExtraKnownImpureNamespaces = extraImpureNamespaces;
            ExtraKnownImpureTypes = extraImpureTypes;
            EnableDebugLogging = enableDebugLogging;
        }

        public static AnalyzerConfiguration FromOptions(AnalyzerOptions options)
        {
            var impureMethods = GetValues(options, ConfigKeys.KnownImpureMethods);
            var pureMethods = GetValues(options, ConfigKeys.KnownPureMethods);
            var impureNamespaces = GetValues(options, ConfigKeys.KnownImpureNamespaces);
            var impureTypes = GetValues(options, ConfigKeys.KnownImpureTypes);
            bool debug = GetBool(options, "purelysharp_enable_debug_logging");
            return new AnalyzerConfiguration(impureMethods, pureMethods, impureNamespaces, impureTypes, debug);
        }

        private static ImmutableHashSet<string> GetValues(AnalyzerOptions options, string key)
        {
            var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            try
            {
                var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (global.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    foreach (var token in value.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var item = token.Trim();
                        if (item.Length > 0)
                        {
                            builder.Add(item);
                        }
                    }
                }
            }
            catch
            {
                // Ignore config parsing issues; default to empty overrides
            }
            return builder.ToImmutable();
        }

        private static bool GetBool(AnalyzerOptions options, string key)
        {
            try
            {
                var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
                if (global.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    if (bool.TryParse(value.Trim(), out var b)) return b;
                    var lowered = value.Trim().ToLowerInvariant();
                    if (lowered == "1" || lowered == "true" || lowered == "yes" || lowered == "on") return true;
                }
            }
            catch { }
            return false;
        }
    }
}