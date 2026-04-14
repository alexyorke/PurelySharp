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
    }
}
