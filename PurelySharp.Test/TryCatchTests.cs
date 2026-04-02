using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class TryCatchTests
    {
        [Test]
        public async Task PureTryCatch_CurrentAnalyzerReportsPS0002()
        {
            var code = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:PureMethod|}()
    {
        try
        {
            int x = 1;
            int y = 0;
            return x / y;
        }
        catch (System.Exception)
        {
            return 0;
        }
        finally
        {
            int z = 1;
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task ImpureTryBody_ReportsDiagnostic()
        {
            var code = @"
using PurelySharp.Attributes;

public class TestClass
{
    private int _val = 0;

    [EnforcePure]
    public int ImpureMethod()
    {
        try
        {
            _val = 1; // Impure
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                .WithSpan(9, 16, 9, 28)
                                .WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(code, expected);
        }

        [Test]
        public async Task ImpureCatchBody_ReportsDiagnostic()
        {
            var code = @"
using PurelySharp.Attributes;

public class TestClass
{
    private int _val = 0;

    [EnforcePure]
    public int ImpureMethod()
    {
        try
        {
            return 1;
        }
        catch
        {
            _val = 1; // Impure
            return 0;
        }
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                .WithSpan(9, 16, 9, 28)
                                .WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(code, expected);
        }
    }
}
