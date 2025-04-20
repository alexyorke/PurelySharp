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
    public int {|PS0002:GetConstant|}() // Explicitly marked for PS0002
    {
        return 5;
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
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
    public int {|PS0002:ImpureMethod|}() // Explicitly marked for PS0002
    {
        _counter++; // Modifies static state
        return _counter;
    }
}";

            // The framework will infer the single expected diagnostic PS0002 
            // from the {|PS0002:...|} markup in the test code.
            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
} 