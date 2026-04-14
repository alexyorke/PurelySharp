using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RuntimeVersioningTests
    {
        [Test]
        public async Task FrameworkNameConstructor_Diagnostic()
        {
            var test = @"
using System.Runtime.Versioning;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FrameworkName {|PS0002:TestMethod|}()
    {
        return new FrameworkName("".NETCoreApp,Version=v8.0"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
