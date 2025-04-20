using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // Need this for [EnforcePure]

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicPurityTests
    {
        [Test]
        public async Task TestPureMethod_NoDiagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes; // Assuming attributes are in this namespace

public class TestClass
{
    [Pure]
    public int GetConstant()
    {
        return 42;
    }
}";

            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task TestEnforcePureMethod_ShouldBeFlaggedNow()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int GetConstant()
    {
        return 5;
    }
}";

            // Expect PS0002 now instead of PS0001
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharp.Analyzer.PurelySharpDiagnostics.PurityNotVerifiedDiagnosticId)
                                             .WithLocation(7, 16) // Line 7, Col 16 (GetConstant)
                                             .WithArguments("GetConstant"); // Only method name argument now

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Test]
        public async Task TestImpureMethod_ShouldBeFlagged_OnMethodName()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public class TestClass
{
    private static int _counter = 0;

    [EnforcePure]
    public int ImpureMethod()
    {
        _counter++; // Modifies static state
        return _counter;
    }
}";

            // Expect PS0002 now instead of PS0001
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharp.Analyzer.PurelySharpDiagnostics.PurityNotVerifiedDiagnosticId)
                                             .WithLocation(10, 16) // Adjusted line number from 9 to 10
                                             .WithArguments("ImpureMethod"); // Only method name argument now

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }
    }
} 