using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PureMethodAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PureMethodAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PMA0001";

        private static readonly LocalizableString Title = "EnforcePure method must have an empty body";
        private static readonly LocalizableString MessageFormat = "Method '{0}' is marked as [EnforcePure] but is not empty";
        private static readonly LocalizableString Description = "Methods marked with [EnforcePure] must have no implementation.";
        private const string Category = "Purity";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // Configure to ignore generated code and enable concurrent execution
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register a syntax node action to analyze method declarations
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Check if the method is marked with [EnforcePure]
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null)
                return;

            bool hasEnforcePureAttribute = methodSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass.Name == "EnforcePureAttribute");

            if (!hasEnforcePureAttribute)
                return;

            // Check if the method body is empty
            bool isEmptyBody = IsMethodBodyEmpty(methodDeclaration);

            if (!isEmptyBody)
            {
                // Report a diagnostic at the method name
                var diagnostic = Diagnostic.Create(
                    Rule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodDeclaration.Identifier.Text);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsMethodBodyEmpty(MethodDeclarationSyntax methodDeclaration)
        {
            // Check for regular method bodies { }
            if (methodDeclaration.Body != null)
            {
                return !methodDeclaration.Body.Statements.Any();
            }

            // Check for expression-bodied methods => 
            if (methodDeclaration.ExpressionBody != null)
            {
                // For now, consider any expression body as non-empty
                return false;
            }

            // Methods without a body (e.g., abstract methods) are considered empty
            return true;
        }
    }
}
