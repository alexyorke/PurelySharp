using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ProcessTests
    {
        [Test]
        public async Task ProcessGetCurrentProcess_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Process {|PS0002:TestMethod|}()
    {
        return Process.GetCurrentProcess();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ProcessId_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(Process process)
    {
        return process.Id;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ProcessStartInfo_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ProcessStartInfo {|PS0002:TestMethod|}(Process process)
    {
        return process.StartInfo;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ProcessExitCode_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(Process process)
    {
        return process.ExitCode;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
