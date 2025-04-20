using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Analyzer.Rules; // Assuming IPurityRule is here
using PurelySharp.Analyzer.Configuration; // Add this
using PurelySharp.Analyzer.Engine;       // Add this
using PurelySharp.Attributes; // Add this
using Microsoft.CodeAnalysis.Operations; // Add this
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PurelySharp.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {
        // Placeholder for the first diagnostic rule
        public const string ImpurityDiagnosticId = "PS0001";
        private static readonly LocalizableString ImpurityTitle = "Impure Method Assumed"; // TODO: Move to Resources
        private static readonly LocalizableString ImpurityMessageFormat = "Method '{0}' marked with [EnforcePure] contains implementation and is assumed impure"; // TODO: Move to Resources
        private static readonly LocalizableString ImpurityDescription = "Methods marked with [EnforcePure] must have their purity explicitly verified or annotated."; // TODO: Move to Resources
        public static readonly DiagnosticDescriptor ImpurityRule = new DiagnosticDescriptor(
            ImpurityDiagnosticId, 
            ImpurityTitle, 
            ImpurityMessageFormat, 
            "Purity", // Category 
            DiagnosticSeverity.Warning, // Default severity 
            isEnabledByDefault: true, 
            description: ImpurityDescription);

        // TODO: Maintain a static, explicit list of all supported IPurityRule types.
        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>(
            // Example: typeof(NoImpureMethodCallRule)
            // Add other rule types here...
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(ImpurityRule); // Return placeholder rule
            // _rules.IsDefaultOrEmpty
            //     ? ImmutableArray<DiagnosticDescriptor>.Empty
            //     : _rules.Select(r => r.Descriptor).ToImmutableArray(); // Restore this later

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); // Or Analyze/ReportDiagnostics

            // Register action for method declarations instead of operations
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Check if method has a body or expression body (i.e., has implementation)
            bool hasImplementation = (methodDeclaration.Body != null && methodDeclaration.Body.Statements.Count > 0) || 
                                     methodDeclaration.ExpressionBody != null;

            if (!hasImplementation)
            {
                return; // No implementation, definitely pure (or abstract/extern)
            }

            // Get the method symbol
            if (!(context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is IMethodSymbol methodSymbol))
            {
                return; // Could not get symbol
            }

            // Get the EnforcePure attribute symbol
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            if (enforcePureAttributeSymbol == null)
            {
                return; // Attribute not found in compilation
            }

            // Check if the method is marked with [EnforcePure]
            if (IsPureEnforced(methodSymbol, enforcePureAttributeSymbol))
            {
                // Report diagnostic on the method name
                var diagnostic = Diagnostic.Create(
                    ImpurityRule, 
                    methodDeclaration.Identifier.GetLocation(), 
                    methodSymbol.Name // Argument {0} is the method name
                    // No second argument needed for this simplified rule
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        // Helper to check if a method is marked with [EnforcePure]
        private static bool IsPureEnforced(IMethodSymbol methodSymbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
             // This helper might need adjustment if we check base types later
             return methodSymbol.GetAttributes().Any(attr =>
                 SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 