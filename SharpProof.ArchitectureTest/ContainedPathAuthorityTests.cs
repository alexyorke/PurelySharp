using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ContainedPathAuthorityTests
{
    [Test]
    public async Task LinuxEvidencePathsUseOrdinalCanonicalContainment()
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            Environment.CurrentDirectory, "Test-SharpProofContainedPathFixtures.ps1");
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

}
