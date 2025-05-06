using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class OutParameterTests
    {
        [Test]
        public async Task PureMethodWithOutParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(out int x)
    {
        x = 10; // Impure operation - writing to an out parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithMultipleOutParameters_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(out int x, out string y)
    {
        x = 10;
        y = ""hello"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithTryPattern_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TryParse|}(string input, out int result)
    {
        if (int.TryParse(input, out result))
        {
            return true;
        }
        result = 0;
        return false;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodCallingMethodWithOutParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private void HelperMethod(out int x)
    {
        x = 42;
    }

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        int result;
        HelperMethod(out result);
        return result;
    }
}";
            var expectedTest = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(8, 18, 8, 30).WithArguments("HelperMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedTest);
        }

        [Test]
        public async Task PureMethodWithOutVarDeclaration_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private void HelperMethod(out int x)
    {
        x = 42;
    }

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        HelperMethod(out var result);
        return result;
    }
}";
            var expectedTest = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(8, 18, 8, 30).WithArguments("HelperMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedTest);
        }

        [Test]
        public async Task PureMethodWithDiscardedOutParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    private void HelperMethod(out int x, out int y)
    {
        x = 42;
        y = 100;
    }

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        int result;
        HelperMethod(out result, out _);
        return result;
    }
}";
            var expectedTest = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(8, 18, 8, 30).WithArguments("HelperMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedTest);
        }
    }
}


