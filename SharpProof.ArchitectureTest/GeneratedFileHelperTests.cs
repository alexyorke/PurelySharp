using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
[Platform("Linux")]
public sealed class GeneratedFileHelperTests
{
    [Test]
    public async Task VerifyRejectsCrlfByteDrift()
    {
        using var temporary = new TempDirectory("SharpProof.GeneratedFileHelper-");
        var fixture = temporary.FullName;
        var probe = Path.Combine(fixture, "probe.ps1");
        var output = Path.Combine(fixture, "generated.txt");
        await File.WriteAllTextAsync(
            probe,
            """
            param(
                [Parameter(Mandatory = $true)][string]$Helper,
                [Parameter(Mandatory = $true)][string]$Output
            )
            Set-StrictMode -Version Latest
            $ErrorActionPreference = 'Stop'
            . $Helper
            $content = "first`nsecond`n"
            Update-SharpProofGeneratedFile `
                -Path $Output `
                -Content $content `
                -DisplayPath 'generated.txt' `
                -GeneratorCommand 'fixture generator'
            Update-SharpProofGeneratedFile `
                -Path $Output `
                -Content $content `
                -DisplayPath 'generated.txt' `
                -GeneratorCommand 'fixture generator' `
                -Verify
            $crlf = [IO.File]::ReadAllText($Output).Replace("`n", "`r`n")
            [IO.File]::WriteAllText(
                $Output,
                $crlf,
                [Text.UTF8Encoding]::new($false))
            try {
                Update-SharpProofGeneratedFile `
                    -Path $Output `
                    -Content $content `
                    -DisplayPath 'generated.txt' `
                    -GeneratorCommand 'fixture generator' `
                    -Verify
            }
            catch {
                exit 0
            }
            throw 'Generated-file verification accepted CRLF byte drift.'
            """);

        var result = await ArchitectureRepository.RunProcessAsync(
            fixture, "pwsh", "-NoLogo", "-NoProfile", "-File", probe,
            "-Helper", Path.Combine(TestRepository.FindRoot(), "scripts", "GeneratedFileHelpers.ps1"),
            "-Output", output);
        Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
    }

    [Test]
    public async Task GeneratedUpdatesUseAtomicSameDirectoryReplacement()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(
            TestRepository.FindRoot(),
            "scripts",
            "GeneratedFileHelpers.ps1"));
        var start = source.IndexOf(
            "function Update-SharpProofGeneratedFile",
            StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        var body = source[start..];

        Assert.That(body, Does.Contain("[System.IO.FileMode]::CreateNew"));
        Assert.That(body, Does.Contain("Flush($true)"));
        Assert.That(body, Does.Contain("[System.IO.File]::Move("));
        Assert.That(body, Does.Contain("finally"));
        Assert.That(body, Does.Not.Contain("[System.IO.File]::WriteAllText("));
    }

}
