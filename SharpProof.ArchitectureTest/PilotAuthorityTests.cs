using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class PilotAuthorityTests
{
    [Test]
    public async Task PilotPackagesAndOutputsUseExactCandidateAuthority()
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            Environment.CurrentDirectory, "Test-SharpProofPilotAuthorityFixtures.ps1");
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

}
