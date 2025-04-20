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
        // Diagnostics moved to PurelySharpDiagnostics.cs

        // TODO: Maintain a static, explicit list of all supported IPurityRule types.
        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>(
            // Example: typeof(NoImpureMethodCallRule)
            // Add other rule types here...
        );

        // Update supported diagnostics to reference the new location
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(PurelySharpDiagnostics.PurityNotVerifiedRule); // Only report PS0002 from this core check for now
            // We might add ImpurityRule (PS0001) back later if specific impurity rules raise it.
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
                // If no implementation, it's definitionally pure (or abstract/partial/extern)
                // OR if [EnforcePure] is *not* present, we don't need to check/report anything here.
                // The check for the attribute happens below *after* confirming implementation exists.
                return; 
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
                 // If marked with [EnforcePure] and has implementation, report "Purity Not Verified"
                 // Later, specific rules will analyze the implementation. If they find impurity, 
                 // they might report PS0001 or a more specific rule. If they prove purity, 
                 // no diagnostic is reported. If no rule can determine the status, PS0002 remains.
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.PurityNotVerifiedRule, // Use the new rule from the diagnostics class
                    methodDeclaration.Identifier.GetLocation(), 
                    methodSymbol.Name // Argument {0} is the method name
                );
                context.ReportDiagnostic(diagnostic);
            }
            // If not marked with [EnforcePure], we don't report anything based on this check.
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