using System;
using System.Collections.Immutable;
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
                    Iterations = 40,
                    Seed = 20260614,
                    OutputDirectory = outputDirectory,
                    CheckpointEvery = 5,
                    Parallelism = 4,
                    Quiet = true
                });

                Assert.That(summary.CasesAnalyzed, Is.EqualTo(40));
                Assert.That(summary.CompilationErrorCount, Is.EqualTo(0));
                Assert.That(summary.OperationKinds, Is.Not.Empty);
                Assert.That(summary.SyntaxKinds, Is.Not.Empty);
                Assert.That(summary.FamilyCounts, Is.Not.Empty);
                Assert.That(summary.Parallelism, Is.EqualTo(4));
                Assert.That(File.Exists(Path.Combine(outputDirectory, "summary.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "coverage.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "summary.partial.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "coverage.partial.json")), Is.True);
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

        [Test]
        public async Task FuzzRunner_RunCasesAsync_DedupesRepeatedInterestingCases_AndCapsSavedCasesPerFamily()
        {
            var outputDirectory = CreateOutputDirectory();
            try
            {
                var repeatedSource = "public class C { public void M() { int value = ; } }";
                var distinctSource = "public class C { MissingType M() => null; }";
                var cases = ImmutableArray.Create(
                    new FuzzCase("CompileFailA", "CompileFail", repeatedSource, AllowUnsafe: false, FuzzExpectation.Conservative()),
                    new FuzzCase("CompileFailB", "CompileFail", repeatedSource, AllowUnsafe: false, FuzzExpectation.Conservative()),
                    new FuzzCase("CompileFailC", "CompileFail", distinctSource, AllowUnsafe: false, FuzzExpectation.Conservative()));

                var summary = await FuzzRunner.RunCasesAsync(
                    cases,
                    new FuzzOptions
                    {
                        OutputDirectory = outputDirectory,
                        MaxInterestingCases = 10,
                        MaxInterestingCasesPerFamily = 1,
                        CheckpointEvery = 2,
                        Parallelism = 4,
                        Quiet = true
                    });

                var interestingCasesDirectory = Path.Combine(outputDirectory, "interesting-cases");
                var savedFiles = Directory.GetFiles(interestingCasesDirectory);
                var occurrenceCounts = summary.Findings
                    .Select(finding => finding.OccurrenceCount)
                    .OrderBy(count => count)
                    .ToArray();

                Assert.That(summary.CasesAnalyzed, Is.EqualTo(3));
                Assert.That(summary.CompilationErrorCount, Is.EqualTo(3));
                Assert.That(summary.FindingCount, Is.EqualTo(3));
                Assert.That(summary.UniqueFindingCount, Is.EqualTo(2));
                Assert.That(summary.InterestingCasesSaved, Is.EqualTo(1));
                Assert.That(savedFiles.Length, Is.EqualTo(1));
                Assert.That(occurrenceCounts, Is.EqualTo(new[] { 1, 2 }));
            }
            finally
            {
                DeleteOutputDirectory(outputDirectory);
            }
        }

        [Test]
        public void FuzzCaseGenerator_DeterministicSample_IncludesExpandedFamilies()
        {
            var generator = new FuzzCaseGenerator(20260614);
            var families = Enumerable.Range(0, 400)
                .Select(index => generator.Next(index).Family)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.That(families, Does.Contain("ImpureAwaitTaskDelay"));
            Assert.That(families, Does.Contain("ImpureLockSection"));
            Assert.That(families, Does.Contain("ImpureUsingStandardOutput"));
            Assert.That(families, Does.Contain("PureInterpolatedString"));
            Assert.That(families, Does.Contain("PureUtf8String"));
            Assert.That(families, Does.Contain("PureArrayCreation"));
            Assert.That(families, Does.Contain("ConservativeSwitchExpression"));
            Assert.That(families, Does.Contain("ConservativeRangeSlice"));
            Assert.That(families, Does.Contain("ConservativeWithExpression"));
            Assert.That(families, Does.Contain("ConservativeImplicitIndexerReference"));
            Assert.That(families, Does.Contain("ConservativeInterpolatedStringHandler"));
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
