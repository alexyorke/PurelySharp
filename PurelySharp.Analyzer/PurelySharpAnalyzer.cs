using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace PurelySharp.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PurelySharpAnalyzer : DiagnosticAnalyzer
    {

        public const string PS0002 = PurelySharpDiagnostics.PurityNotVerifiedId;
        public const string PS0004 = PurelySharpDiagnostics.MissingEnforcePureAttributeId;

        private static readonly ImmutableArray<Type> _ruleTypes = ImmutableArray.Create<Type>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(PurelySharpDiagnostics.PurityNotVerifiedRule,
                                  PurelySharpDiagnostics.MisplacedAttributeRule,
                                  PurelySharpDiagnostics.MissingEnforcePureAttributeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(MethodPurityAnalyzer.AnalyzeSymbolForPurity,
                SyntaxKind.MethodDeclaration,
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.OperatorDeclaration,
                SyntaxKind.LocalFunctionStatement);

            var analyzedKinds = ImmutableHashSet.Create(SyntaxKind.MethodDeclaration,
                                                        SyntaxKind.GetAccessorDeclaration,
                                                        SyntaxKind.SetAccessorDeclaration,
                                                        SyntaxKind.ConstructorDeclaration,
                                                        SyntaxKind.OperatorDeclaration,
                                                        SyntaxKind.LocalFunctionStatement);

            var allKindsExceptAnalyzed = Enum.GetValues(typeof(SyntaxKind))
                                           .Cast<SyntaxKind>()
                                           .Where(k => !analyzedKinds.Contains(k))
                                           .ToImmutableArray();

            context.RegisterSyntaxNodeAction(AttributePlacementAnalyzer.AnalyzeNonMethodDeclaration, allKindsExceptAnalyzed);
        }
    }
}