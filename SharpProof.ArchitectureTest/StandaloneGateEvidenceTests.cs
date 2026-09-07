using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class StandaloneGateEvidenceTests
{
    [Test]
    public async Task StandaloneGateDecoderRejectsUnauthenticatedEvidence()
    {
        var root = TestRepository.FindRoot();
        var result = await ArchitectureRepository.RunScriptAsync(
            root, "Test-SharpProofStandaloneGateEvidence.ps1");
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

    [Test]
    public void StandaloneGateProducerIsFreshBuildAndIdentityBound()
    {
        var root = TestRepository.FindRoot();
        var evidence = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-SharpProofGateEvidence.ps1"));
        var producer = File.ReadAllText(Path.Combine(
            root,
            "SharpProof.Gates",
            "Program.cs"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evidence, Does.Contain("-t:Rebuild"));
            Assert.That(evidence, Does.Not.Contain("'--no-build'"));
            Assert.That(
                evidence,
                Does.Contain("Assert-SharpProofStandaloneGateResult"));
            Assert.That(evidence, Does.Contain("SharpProofSourceCommit"));
            Assert.That(
                evidence,
                Does.Contain("Get-SharpProofModuleVersionId"));
            Assert.That(producer, Does.Contain("CreateStandaloneEnvelope"));
            Assert.That(producer, Does.Contain("ModuleVersionId"));
            Assert.That(producer, Does.Not.Contain("SHA256"));
        }
    }

}
