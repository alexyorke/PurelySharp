using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PurelySharp.Attributes;
using System.Collections.Generic;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer
{
    internal static class MethodPurityAnalyzer
    {
        internal static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
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

            bool isPureEnforced = PurityAnalysisEngine.IsPureEnforced(methodSymbol, enforcePureAttributeSymbol);
            bool isConsideredPure = PurityAnalysisEngine.IsConsideredPure(methodSymbol, context, enforcePureAttributeSymbol, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));

            if (isPureEnforced)
            {
                if (!isConsideredPure)
                {
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.PurityNotVerifiedRule,
                        methodDeclaration.Identifier.GetLocation(),
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
            else
            {
                if (isConsideredPure)
                {
                    var diagnostic = Diagnostic.Create(
                        PurelySharpDiagnostics.MissingEnforcePureAttributeRule,
                        methodDeclaration.Identifier.GetLocation(),
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}