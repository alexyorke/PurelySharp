using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Analyzer.Rules;
using PurelySharp.Analyzer.Configuration;
using PurelySharp.Analyzer.Engine;
using PurelySharp.Attributes;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PurelySharp.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>(
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(PurelySharpDiagnostics.PurityNotVerifiedRule,
                                  PurelySharpDiagnostics.MisplacedAttributeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);

            var allKindsExceptMethod = Enum.GetValues(typeof(SyntaxKind))
                                           .Cast<SyntaxKind>()
                                           .Where(k => k != SyntaxKind.MethodDeclaration)
                                           .ToImmutableArray();

            context.RegisterSyntaxNodeAction(AnalyzeNonMethodDeclaration, allKindsExceptMethod);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            bool hasImplementation = (methodDeclaration.Body != null && methodDeclaration.Body.Statements.Count > 0) ||
                                     methodDeclaration.ExpressionBody != null;

            if (!hasImplementation)
            {
                return;
            }

            if (!(context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is IMethodSymbol methodSymbol))
            {
                return;
            }

            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            if (enforcePureAttributeSymbol == null)
            {
                return;
            }

            if (IsPureEnforced(methodSymbol, enforcePureAttributeSymbol))
            {
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.PurityNotVerifiedRule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeNonMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var enforcePureAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName);
            if (enforcePureAttributeSymbol == null)
            {
                return;
            }

            Location? attributeLocation = null;
            bool attributeFoundOnNode = false;

            if (context.Node is MemberDeclarationSyntax memberDecl)
            {
                attributeLocation = FindAttributeLocation(memberDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                attributeFoundOnNode = attributeLocation != null;
            }
            else if (context.Node is TypeDeclarationSyntax typeDecl)
            {
                 attributeLocation = FindAttributeLocation(typeDecl.AttributeLists, enforcePureAttributeSymbol, context.SemanticModel);
                 attributeFoundOnNode = attributeLocation != null;
            }

            if (attributeFoundOnNode)
            {
                var diagnostic = Diagnostic.Create(
                    PurelySharpDiagnostics.MisplacedAttributeRule,
                    attributeLocation!
                );
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }

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

        private static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
             return symbol.GetAttributes().Any(attr =>
                 SymbolEqualityComparer.Default.Equals(attr.AttributeClass, enforcePureAttributeSymbol));
        }
    }
} 