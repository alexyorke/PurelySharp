using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class PublicationPlanTopologyTests
{
    [TestCase("valid-disjoint", true)]
    [TestCase("existing-output", true)]
    [TestCase("main-package", false)]
    [TestCase("symbol-package", false)]
    [TestCase("manifest", false)]
    [TestCase("fixture-input", false)]
    [TestCase("relative-dot-alias", false)]
    [TestCase("absolute-alias", false)]
    [TestCase("symlink-alias", false)]
    [TestCase("hardlink-alias", false)]
    [TestCase("reserved-name", false)]
    [TestCase("writer-failure", false)]
    [TestCase("post-write-mutation", false)]
    public async Task PlanOutputCannotAliasOrInvalidateCertifiedInputs(
        string mutation,
        bool expectedSuccess)
    {
        var result = await ArchitectureRepository.RunScriptAsync(
            TestRepository.FindRoot(), "Test-SharpProofPublicationPlanTopologyFixtures.ps1",
            "-Mutation", mutation);
        Assert.That(
            result.ExitCode == 0,
            Is.EqualTo(expectedSuccess),
            result.CombinedOutput);
    }

    [Test]
    public async Task PublisherUsesAtomicTopologyAuthorityBeforeValidation()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(
            TestRepository.FindRoot(), "scripts", "Publish-SharpProofRelease.ps1"));
        var resolve = script.IndexOf(
            "Resolve-SharpProofPublicationPlanOutput",
            StringComparison.Ordinal);
        var validate = script.IndexOf(
            "Get-ValidatedRelease",
            resolve + 1,
            StringComparison.Ordinal);
        Assert.That(resolve, Is.GreaterThanOrEqualTo(0));
        Assert.That(validate, Is.GreaterThan(resolve));
        Assert.That(script, Does.Contain("Write-SharpProofPublicationPlanAtomic"));
    }
}
