using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace PureMethodAnalyzer.Test
{
    public static partial class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public class Test : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
        {
            public Test()
            {
                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    if (project == null) return solution;

                    var compilationOptions = project.CompilationOptions;
                    if (compilationOptions == null) return solution;

                    compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                        compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));

                    return solution.WithProjectCompilationOptions(projectId, compilationOptions);
                });
            }
        }
    }
}
