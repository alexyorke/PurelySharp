using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
    [EnforcePure] // Use EnforcePure instead of Pure
    public int GetConstant()
    {
        return 42;
    }
}";

            // Expect PS0001 diagnostic on the method name because it contains implementation
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpAnalyzer.ImpurityRule)
                                             .WithLocation(7, 16) // Line 7, Col 16 (GetConstant)
                                             .WithArguments("GetConstant"); // Only method name argument now

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Test]
        public async Task TestImpureMethod_ShouldBeFlagged_OnMethodName()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class TestClass
{
    private static int _counter = 0;

    [EnforcePure]
    public int ImpureMethod()
    {
        _counter++; // Impure operation: Modifying static state
        return _counter;
    }
}";

            // Expect PS0001 diagnostic on the method name because it contains implementation
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpAnalyzer.ImpurityRule)
                                             .WithLocation(9, 16) // Line 9, Col 16 (ImpureMethod)
                                             .WithArguments("ImpureMethod"); // Only method name argument now

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }
    }
} 