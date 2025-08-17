using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer
{

    public static class PurelySharpDiagnostics
    {

        public const string ImpurityDiagnosticId = "PS0001";
        private static readonly LocalizableString ImpurityTitle = "Impure Method Assumed";
        private static readonly LocalizableString ImpurityMessageFormat = "Method '{0}' marked with [EnforcePure] contains implementation and is assumed impure";
        private static readonly LocalizableString ImpurityDescription = "Methods marked with [EnforcePure] must have their purity explicitly verified or annotated.";



        public static readonly DiagnosticDescriptor ImpurityRule = new DiagnosticDescriptor(
            ImpurityDiagnosticId,
            ImpurityTitle,
            ImpurityMessageFormat,
            "Purity",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: ImpurityDescription);


        public const string PurityNotVerifiedId = "PS0002";
        private static readonly LocalizableString PurityNotVerifiedTitle = "Purity Not Verified";
        private static readonly LocalizableString PurityNotVerifiedMessageFormat = "Method '{0}' marked with [EnforcePure] has implementation, but its purity has not been verified by existing rules";
        private static readonly LocalizableString PurityNotVerifiedDescription = "Methods marked with [EnforcePure] require analysis. This diagnostic indicates the analysis rules did not determine the method's purity status.";

        public static readonly DiagnosticDescriptor PurityNotVerifiedRule = new DiagnosticDescriptor(
            id: PurityNotVerifiedId,
            title: PurityNotVerifiedTitle,
            messageFormat: PurityNotVerifiedMessageFormat,
            category: "Purity",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: PurityNotVerifiedDescription);


        public const string MisplacedAttributeId = "PS0003";
        private static readonly LocalizableString MisplacedAttributeTitle = "Misplaced [EnforcePure] Attribute";
        private static readonly LocalizableString MisplacedAttributeMessageFormat = "The [EnforcePure] attribute can only be applied to method declarations";
        private static readonly LocalizableString MisplacedAttributeDescription = "[EnforcePure] should only be used on methods to indicate they require purity analysis.";

        public static readonly DiagnosticDescriptor MisplacedAttributeRule = new DiagnosticDescriptor(
            id: MisplacedAttributeId,
            title: MisplacedAttributeTitle,
            messageFormat: MisplacedAttributeMessageFormat,
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: MisplacedAttributeDescription);


        public const string MissingEnforcePureAttributeId = "PS0004";
        private static readonly LocalizableString MissingEnforcePureAttributeTitle = "Missing [EnforcePure] Attribute";
        private static readonly LocalizableString MissingEnforcePureAttributeMessageFormat = "Method '{0}' appears to be pure but is not marked with [EnforcePure]. Consider adding the attribute to enforce and document its purity.";
        private static readonly LocalizableString MissingEnforcePureAttributeDescription = "This method seems to only contain operations considered pure, but it lacks the [EnforcePure] attribute. Adding the attribute helps ensure its purity is maintained and communicates intent.";

        public static readonly DiagnosticDescriptor MissingEnforcePureAttributeRule = new DiagnosticDescriptor(
            id: MissingEnforcePureAttributeId,
            title: MissingEnforcePureAttributeTitle,
            messageFormat: MissingEnforcePureAttributeMessageFormat,
            category: "Purity",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: MissingEnforcePureAttributeDescription);
    }
}