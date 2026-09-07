using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ReleaseVersionAuthorityTests
{
    [TestCase("canonical", true)]
    [TestCase("foreign-matching", false)]
    [TestCase("case-only-prerelease", false)]
    [TestCase("mixed-package", false)]
    [TestCase("stale-manifest", false)]
    [TestCase("stale-plan", false)]
    public async Task ReleaseVersionProjectionIsOwnedByReleaseProps(
        string mutation,
        bool expectedSuccess)
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            TestRepository.FindRoot(), "Test-SharpProofReleaseVersionAuthorityFixtures.ps1",
            "-Mutation", mutation);
        Assert.That(
            result.ExitCode == 0,
            Is.EqualTo(expectedSuccess),
            result.CombinedOutput);
    }

    [Test]
    public async Task EveryReleaseEntryPointUsesTheSharedVersionAuthority()
    {
        var root = TestRepository.FindRoot();
        foreach (var relative in new[]
                 {
                     "scripts/New-SharpProofReleaseEvidence.ps1",
                     "scripts/Test-SharpProofReleaseArtifacts.ps1",
                     "scripts/Publish-SharpProofRelease.ps1",
                     "scripts/Invoke-SharpProofReleaseContainer.ps1"
                 })
        {
            var text = await File.ReadAllTextAsync(Path.Combine(
                root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            Assert.That(
                text,
                Does.Contain("Get-SharpProofReleaseVersion"),
                relative);
        }
    }
}
