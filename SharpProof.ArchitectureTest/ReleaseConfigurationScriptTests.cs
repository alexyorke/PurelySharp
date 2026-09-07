using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ReleaseConfigurationScriptTests
{
    [Test]
    public async Task EffectiveReleaseRefSetsMustEqualTheContract()
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            Environment.CurrentDirectory, "Test-SharpProofReleaseConfigurationFixtures.ps1");
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

}
