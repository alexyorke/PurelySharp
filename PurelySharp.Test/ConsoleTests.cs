using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConsoleTests
    {
        [Test]
        public async Task ConsoleOut_Diagnostic()
        {
            var test = @"
using System;
using System.IO;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TextWriter {|PS0002:TestMethod|}()
    {
        return Console.Out;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConsoleError_Diagnostic()
        {
            var test = @"
using System;
using System.IO;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TextWriter {|PS0002:TestMethod|}()
    {
        return Console.Error;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
