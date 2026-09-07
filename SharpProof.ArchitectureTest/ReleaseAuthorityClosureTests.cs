using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ReleaseAuthorityClosureTests
{
    [TestCase("Test-SharpProofReleaseAuthorityClosure.ps1")]
    [TestCase("Test-SharpProofReleaseAuthorityClosureFixtures.ps1")]
    public async Task ReleaseAuthorityClosureIsIndependentAndMutationDiscriminating(
        string scriptName)
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            Environment.CurrentDirectory, scriptName);
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

}
