using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using PurelySharp.Tools.Fuzz;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FuzzToolTests
    {
        [Test]
        public async Task FuzzRunner_SmokeRun_WritesSummaryAndCoverageArtifacts()
        {
            var outputDirectory = CreateOutputDirectory();
            try
            {
                var summary = await FuzzRunner.RunAsync(new FuzzOptions
                {
                    Iterations = 12,
                    Seed = 123,
                    OutputDirectory = outputDirectory,
                    Quiet = true
                });

                Assert.That(summary.CasesAnalyzed, Is.EqualTo(12));
                Assert.That(summary.CompilationErrorCount, Is.EqualTo(0));
                Assert.That(summary.OperationKinds, Is.Not.Empty);
                Assert.That(summary.SyntaxKinds, Is.Not.Empty);
                Assert.That(summary.FamilyCounts, Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "summary.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "coverage.json")), Is.True);
            }
            finally
            {
                DeleteOutputDirectory(outputDirectory);
            }
        }

        [Test]
        public async Task FuzzRunner_KnownImpureConsoleCase_ProducesPs0002()
        {
            var source = """
using System;
using PurelySharp.Attributes;

public class KnownImpureConsoleCase
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine("impure");
    }
}
""";

            var analysis = await FuzzRunner.AnalyzeCaseAsync(new FuzzCase(
                "KnownImpureConsoleCase",
                "KnownImpureConsole",
                source,
                AllowUnsafe: false,
                FuzzExpectation.DefinitelyImpure()));

            Assert.That(analysis.CompilationErrors, Is.Empty);
            Assert.That(analysis.Diagnostics.Any(diagnostic => diagnostic.Id == PurelySharpDiagnostics.PurityNotVerifiedId), Is.True);
            Assert.That(analysis.Findings.Any(finding => finding.Category == "impure_missing_ps0002"), Is.False);
            Assert.That(analysis.OperationKinds.ContainsKey("Invocation"), Is.True);
        }

        private static string CreateOutputDirectory()
        {
            var outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "fuzz-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        private static void DeleteOutputDirectory(string outputDirectory)
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }
}
