using System.Xml.Linq;
using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class GeneratedCodeAnalysisConfigurationTests
{
    [Test]
    public void EveryRoslynAnalyzerConfiguresGeneratedCodeAnalysis()
    {
        var analyzers = ArchitectureRepository.ProductionProjects
            .Append("SharpProof.CompilerProbe.TestAsset")
            .Where(static project => File.Exists(
                ArchitectureRepository.ProjectFile(project)))
            .Where(static project => XDocument.Load(
                    ArchitectureRepository.ProjectFile(project))
                .Descendants("IsRoslynAnalyzer")
                .Any(static element =>
                    string.Equals(
                        element.Value.Trim(),
                        "true",
                        StringComparison.OrdinalIgnoreCase)))
            .SelectMany(ArchitectureRepository.ProductionSourceFiles)
            .Select(static path => new
            {
                Path = path,
                Source = File.ReadAllText(path)
            })
            .Where(static item => item.Source.Contains(
                "[DiagnosticAnalyzer(",
                StringComparison.Ordinal))
            .ToArray();

        Assert.That(analyzers, Is.Not.Empty,
            "The inventory must discover the product's Roslyn analyzers.");

        var invalid = analyzers
            .Where(static item =>
            {
                var calls = item.Source
                    .Split("ConfigureGeneratedCodeAnalysis", StringSplitOptions.None)
                    .Length - 1;
                return calls != 1 ||
                    !item.Source.Contains(
                        "GeneratedCodeAnalysisFlags.Analyze |",
                        StringComparison.Ordinal) ||
                    !item.Source.Contains(
                        "GeneratedCodeAnalysisFlags.ReportDiagnostics",
                        StringComparison.Ordinal);
            })
            .Select(static item => item.Path)
            .ToArray();

        Assert.That(
            invalid,
            Is.Empty,
            "Every Roslyn analyzer must opt into the product generated-code policy.");
    }
}
