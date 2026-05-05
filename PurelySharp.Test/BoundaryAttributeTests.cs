using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BoundaryAttributeTests
    {
        [Test]
        public async Task PureExternal_Method_IsTrustedAtCallSite()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [PureExternal]
    public static int TrustedBoundary() => DateTime.Now.Millisecond;

    [EnforcePure]
    public int Caller() => TrustedBoundary();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_Method_IsImpureAtCallSiteEvenWithPureBody()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [Impure]
    public static int ExplicitlyImpure() => 1;

    [EnforcePure]
    public int {|PS0002:Caller|}() => ExplicitlyImpure();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_WithEnforcePure_ReportsDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [Impure]
    [EnforcePure]
    public int {|PS0002:Contradiction|}() => 1;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
