using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ReleaseTagValidationTests
{
    [Test]
    public async Task ReleaseTagAuthorityRejectsEveryNonExactIdentity()
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            Environment.CurrentDirectory, "Test-SharpProofReleaseTagFixtures.ps1");
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

}
