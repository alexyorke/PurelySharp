using NUnit.Framework;
using PurelySharp.Analyzer;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ExceptionCatalogTests
    {
        [Test]
        public async Task FileNotFoundExceptionStringConstructor_NoDiagnostic()
        {
            var test = @"
using System.IO;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FileNotFoundException TestMethod()
    {
        return new FileNotFoundException(""missing.txt"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
