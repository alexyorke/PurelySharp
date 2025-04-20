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

        // Update supported diagnostics to reference the new location and include the new rule
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(PurelySharpDiagnostics.PurityNotVerifiedRule, 
                                  PurelySharpDiagnostics.MisplacedAttributeRule); 

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); 

            // Register action for method declarations (for PS0002) FIRST
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);

            // Register actions for all other declaration types (for PS0003) SECOND
            // Programmatically get all SyntaxKind values except MethodDeclaration
            var allKindsExceptMethod = Enum.GetValues(typeof(SyntaxKind))
                                           .Cast<SyntaxKind>()
                                           .Where(k => k != SyntaxKind.MethodDeclaration)
                                           // Consider adding more filters here if needed (e.g., for performance)
                                           // .Where(k => IsRelevantKind(k)) // Example filter
                                           .ToImmutableArray();

            context.RegisterSyntaxNodeAction(AnalyzeNonMethodDeclaration, allKindsExceptMethod);
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

        private void AnalyzeNonMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            // --- Remove DEBUGGING --- 
            // if (context.Node is Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax fieldDecl) 
            // { ... }
            // --- END DEBUGGING ---

            // Get the EnforcePure attribute symbol first
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            if (enforcePureAttributeSymbol == null)
            {
                return; // Attribute not found in compilation
            }

            // *** Check attributes directly on the Syntax Node ***
            Location? attributeLocation = null;
            bool attributeFoundOnNode = false;

            if (context.Node is MemberDeclarationSyntax memberDecl) // Covers Field, Property, Event, etc.
            {
                attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                attributeFoundOnNode = attributeLocation != null;
            }
            else if (context.Node is TypeDeclarationSyntax typeDecl) // Covers Class, Struct, Interface
            {
                 attributeLocation = FindAttributeLocation(typeDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                 attributeFoundOnNode = attributeLocation != null;
            }
            // Add checks for other specific node types with attributes if necessary

            if (attributeFoundOnNode)
            {
                 // Report PS0003 directly if attribute found on the node syntax
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.MisplacedAttributeRule,
                    attributeLocation! // We know it's not null here
                    // No arguments needed
                );
                context.ReportDiagnostic(diagnostic);
                // Return because we don't need the fallback check below if we found it here.
                // If we didn't find it here, the fallback check wouldn't find it on the symbol either
                // (assuming attributes are correctly propagated from syntax to symbol).
                return; 
            }

            // --- Removed Fallback Logic Block --- 

        }

        // Helper to find the specific location of the target attribute
        private static Location? FindAttributeLocation(SyntaxList<AttributeListSyntax> attributeLists, INamedTypeSymbol targetAttributeSymbol, SemanticModel semanticModel)
        {
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                    if (symbolInfo.Symbol is IMethodSymbol attributeConstructorSymbol)
                    {
                        if (SymbolEqualityComparer.Default.Equals(attributeConstructorSymbol.ContainingType, targetAttributeSymbol))
                        {
                            return attribute.GetLocation();
                        }
                    }
                }
            }
            return null;
        }

        // Helper to check if a symbol is marked with [EnforcePure]
        // Updated to accept any ISymbol
        private static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
             return symbol.GetAttributes().Any(attr =>
                 SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 