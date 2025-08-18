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

        private AnalyzerConfiguration(
            ImmutableHashSet<string> extraImpureMethods,
            ImmutableHashSet<string> extraPureMethods,
            ImmutableHashSet<string> extraImpureNamespaces,
            ImmutableHashSet<string> extraImpureTypes)
        {
            ExtraKnownImpureMethods = extraImpureMethods;
            ExtraKnownPureMethods = extraPureMethods;
            ExtraKnownImpureNamespaces = extraImpureNamespaces;
            ExtraKnownImpureTypes = extraImpureTypes;
        }

        public static AnalyzerConfiguration FromOptions(AnalyzerOptions options)
        {
            var impureMethods = GetValues(options, ConfigKeys.KnownImpureMethods);
            var pureMethods = GetValues(options, ConfigKeys.KnownPureMethods);
            var impureNamespaces = GetValues(options, ConfigKeys.KnownImpureNamespaces);
            var impureTypes = GetValues(options, ConfigKeys.KnownImpureTypes);
            return new AnalyzerConfiguration(impureMethods, pureMethods, impureNamespaces, impureTypes);
        }

        private static ImmutableHashSet<string> GetValues(AnalyzerOptions options, string key)
        {
            var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            // Placeholder: AnalyzerConfigOptionsProvider can be threaded through here to read .editorconfig options
            return builder.ToImmutable();
        }
    }
}