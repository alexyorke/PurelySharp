using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StackTraceTests
    {
        [Test]
        public async Task StackFrameGetMethod_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Diagnostics;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodBase? {|PS0002:TestMethod|}(StackFrame stackFrame)
    {
        return stackFrame.GetMethod();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
