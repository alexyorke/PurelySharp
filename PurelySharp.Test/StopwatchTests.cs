using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StopwatchTests
    {
        [Test]
        public async Task StopwatchIsRunning_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Stopwatch stopwatch)
    {
        return stopwatch.IsRunning;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StopwatchConstructor_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Stopwatch {|PS0002:TestMethod|}()
    {
        return new Stopwatch();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
