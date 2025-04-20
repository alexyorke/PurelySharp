using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace PurelySharp.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(PurelySharpDiagnostics.PurityNotVerifiedRule,
                                  PurelySharpDiagnostics.MisplacedAttributeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(MethodPurityAnalyzer.AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);

            var allKindsExceptMethod = Enum.GetValues(typeof(SyntaxKind))
                                           .Cast<SyntaxKind>()
                                           .Where(k => k != SyntaxKind.MethodDeclaration)
                                           .ToImmutableArray();

            context.RegisterSyntaxNodeAction(AttributePlacementAnalyzer.AnalyzeNonMethodDeclaration, allKindsExceptMethod);
        }
    }
} 