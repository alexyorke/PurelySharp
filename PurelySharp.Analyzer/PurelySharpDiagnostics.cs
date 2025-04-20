using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer
{
    /// <summary>
    /// Contains the diagnostic descriptors for the PurelySharp analyzer.
    /// </summary>
    public static class PurelySharpDiagnostics
    {
        // --- PS0001: Impure Method Assumed ---
        public const string ImpurityDiagnosticId = "PS0001";
        private static readonly LocalizableString ImpurityTitle = "Impure Method Assumed"; // TODO: Move to Resources
        private static readonly LocalizableString ImpurityMessageFormat = "Method '{0}' marked with [EnforcePure] contains implementation and is assumed impure"; // TODO: Move to Resources
        private static readonly LocalizableString ImpurityDescription = "Methods marked with [EnforcePure] must have their purity explicitly verified or annotated."; // TODO: Move to Resources

        // Note: While PS0001 is defined, it's not currently raised by the core analyzer logic (PurelySharpAnalyzer).
        // It might be used by specific impurity detection rules later.
        public static readonly DiagnosticDescriptor ImpurityRule = new DiagnosticDescriptor(
            ImpurityDiagnosticId,
            ImpurityTitle,
            ImpurityMessageFormat,
            "Purity",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: ImpurityDescription);

        // --- PS0002: Purity Not Verified ---
        public const string PurityNotVerifiedDiagnosticId = "PS0002";
        private static readonly LocalizableString PurityNotVerifiedTitle = "Purity Not Verified"; // TODO: Move to Resources
        private static readonly LocalizableString PurityNotVerifiedMessageFormat = "Method '{0}' marked with [EnforcePure] has implementation, but its purity has not been verified by existing rules"; // TODO: Move to Resources
        private static readonly LocalizableString PurityNotVerifiedDescription = "Methods marked with [EnforcePure] require analysis. This diagnostic indicates the analysis rules did not determine the method's purity status."; // TODO: Move to Resources

        public static readonly DiagnosticDescriptor PurityNotVerifiedRule = new DiagnosticDescriptor(
            PurityNotVerifiedDiagnosticId,
            PurityNotVerifiedTitle,
            PurityNotVerifiedMessageFormat,
            "Purity",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: PurityNotVerifiedDescription);

        // --- PS0003: Misplaced [EnforcePure] Attribute ---
        public const string MisplacedAttributeDiagnosticId = "PS0003";
        private static readonly LocalizableString MisplacedAttributeTitle = "Misplaced [EnforcePure] Attribute"; // TODO: Move to Resources
        private static readonly LocalizableString MisplacedAttributeMessageFormat = "The [EnforcePure] attribute can only be applied to method declarations"; // TODO: Move to Resources
        private static readonly LocalizableString MisplacedAttributeDescription = "[EnforcePure] should only be used on methods to indicate they require purity analysis."; // TODO: Move to Resources

        public static readonly DiagnosticDescriptor MisplacedAttributeRule = new DiagnosticDescriptor(
            MisplacedAttributeDiagnosticId,
            MisplacedAttributeTitle,
            MisplacedAttributeMessageFormat,
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: MisplacedAttributeDescription);
    }
} 