using NUnit.Framework;

namespace SharpProof.ArchitectureTest;

[TestFixture]
public sealed class ProcessRunnerTests
{
    [Test]
    public void CreateStartInfoConfiguresCaptureAndPreservesArgumentOrder()
    {
        var root = TestRepository.FindRoot();
        var arguments = new[] { "-NoLogo", "-NoProfile", "-Command", "exit 0" };
        var startInfo = ProcessRunner.CreateStartInfo(root, "pwsh", arguments);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(startInfo.FileName, Is.EqualTo("pwsh"));
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo(root));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.RedirectStandardOutput, Is.True);
            Assert.That(startInfo.RedirectStandardError, Is.True);
            Assert.That(startInfo.CreateNoWindow, Is.True);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(arguments));
        }
    }

    [Test]
    public async Task CapturesDistinctStreamsAndNonzeroExit()
    {
        var result = await ProcessRunner.RunCapturedAsync(
            TestRepository.FindRoot(),
            "pwsh",
            "-NoLogo",
            "-NoProfile",
            "-Command",
            "[Console]::Out.Write('stdout'); " +
            "[Console]::Error.Write('stderr'); exit 7");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(7));
            Assert.That(result.Output, Is.EqualTo("stdout"));
            Assert.That(result.Error, Is.EqualTo("stderr"));
            Assert.That(
                result.CombinedOutput,
                Is.EqualTo("stdout" + Environment.NewLine + "stderr"));
        }
    }
}
