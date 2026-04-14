using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DiagnosticsTests
    {
        [Test]
        public async Task FileVersionInfoFileVersion_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(FileVersionInfo fileVersionInfo)
    {
        return fileVersionInfo.FileVersion;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
