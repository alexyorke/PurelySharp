namespace PurelySharp.Analyzer.Configuration
{

    internal static class ConfigKeys
    {
        public const string KnownImpureMethods = "purelysharp_known_impure_methods";
        public const string KnownPureMethods = "purelysharp_known_pure_methods";
        public const string KnownImpureNamespaces = "purelysharp_known_impure_namespaces";
        public const string KnownImpureTypes = "purelysharp_known_impure_types";

        /// <summary>When false, PS0004 (missing [EnforcePure]) is not reported. Default: true.</summary>
        public const string SuggestMissingEnforcePure = "purelysharp_suggest_missing_enforce_pure";
    }
}