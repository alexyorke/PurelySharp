using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Core
{
    /// <summary>
    /// Default implementation of IPurityAnalyzer that wraps the existing functionality.
    /// </summary>
    public class DefaultPurityAnalyzer : IPurityAnalyzer
    {
        public PurityAnalysisResult Analyze(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, PurityContext context)
        {
            var impurityLocations = new List<Location>();
            var impurityReasons = new Dictionary<Location, string>();

            // Check for impure operations in the method body
            if (methodDeclaration.Body != null)
            {
                // Check for unsafe code
                var unsafeStatements = methodDeclaration.Body.DescendantNodes()
                    .OfType<UnsafeStatementSyntax>()
                    .ToList();
                foreach (var unsafeStmt in unsafeStatements)
                {
                    impurityLocations.Add(unsafeStmt.GetLocation());
                    impurityReasons[unsafeStmt.GetLocation()] = "Method contains unsafe code";
                }

                // Check for fixed statements
                var fixedStatements = methodDeclaration.Body.DescendantNodes()
                    .OfType<FixedStatementSyntax>()
                    .ToList();
                foreach (var fixedStmt in fixedStatements)
                {
                    impurityLocations.Add(fixedStmt.GetLocation());
                    impurityReasons[fixedStmt.GetLocation()] = "Method contains fixed statement";
                }

                // Check for using statements with disposable objects
                var usingStatements = methodDeclaration.Body.DescendantNodes()
                    .OfType<UsingStatementSyntax>()
                    .ToList();
                foreach (var usingStmt in usingStatements)
                {
                    impurityLocations.Add(usingStmt.GetLocation());
                    impurityReasons[usingStmt.GetLocation()] = "Method contains using statement with disposable object";
                }

                // Check for field assignments
                var assignments = methodDeclaration.Body.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.Left is MemberAccessExpressionSyntax)
                    .ToList();
                foreach (var assignment in assignments)
                {
                    impurityLocations.Add(assignment.GetLocation());
                    impurityReasons[assignment.GetLocation()] = "Method contains field assignment";
                }

                // Check for mutable parameters
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
                if (methodSymbol != null)
                {
                    foreach (var parameter in methodSymbol.Parameters)
                    {
                        if (parameter.RefKind != RefKind.None)
                        {
                            var paramSyntax = methodDeclaration.ParameterList.Parameters
                                .FirstOrDefault(p => p.Identifier.Text == parameter.Name);
                            if (paramSyntax != null)
                            {
                                impurityLocations.Add(paramSyntax.GetLocation());
                                impurityReasons[paramSyntax.GetLocation()] = $"Parameter '{parameter.Name}' is {parameter.RefKind}";
                            }
                        }
                    }
                }
            }

            // Check for impure operations in expressions
            var expressions = methodDeclaration.DescendantNodes()
                .OfType<ExpressionSyntax>()
                .ToList();
            foreach (var expr in expressions)
            {
                if (!ExpressionPurityChecker.IsExpressionPure(expr, semanticModel, null))
                {
                    impurityLocations.Add(expr.GetLocation());
                    impurityReasons[expr.GetLocation()] = "Expression contains impure operations";
                }
            }

            // Check for impure statements
            if (methodDeclaration.Body != null && !StatementPurityChecker.AreStatementsPure(methodDeclaration.Body.Statements, semanticModel, null))
            {
                impurityLocations.Add(methodDeclaration.Body.GetLocation());
                impurityReasons[methodDeclaration.Body.GetLocation()] = "Method body contains impure operations";
            }

            if (impurityLocations.Count > 0)
            {
                return PurityAnalysisResult.Impure(impurityLocations.ToImmutableArray(), impurityReasons.ToImmutableDictionary());
            }

            return PurityAnalysisResult.Pure();
        }
    }
}