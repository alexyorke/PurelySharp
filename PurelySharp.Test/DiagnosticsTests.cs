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

        [Test]
        public async Task ActivitySourceConstructor_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ActivitySource {|PS0002:TestMethod|}()
    {
        return new ActivitySource(""test"", ""1.0.0"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DiagnosticListenerConstructor_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DiagnosticListener {|PS0002:TestMethod|}()
    {
        return new DiagnosticListener(""test"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DebugAssert_Diagnostic()
        {
            var test = @"
using System.Diagnostics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        Debug.Assert(true);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
