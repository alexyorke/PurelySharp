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

        [Test]
        public async Task StackTraceConstructor_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public StackTrace {|PS0002:TestMethod|}()
    {
        return new StackTrace();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
