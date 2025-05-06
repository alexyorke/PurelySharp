using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LocalFunctionAndRecursionTests
    {
        [Test]
        public async Task ImpureLocalFunction_FieldModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _field;

    [EnforcePure]
    public int TestMethod()
    {
        int LocalFunction()
        {
            _field++; // Local function modifies field
            return _field;
        }

        return LocalFunction();
    }
}";

            // Expect diagnostic on TestMethod due to impure local function
            var expectedOuter = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(12, 16, 12, 26) // ACTUAL Span reported by test runner (attribute line)
                                   .WithArguments("TestMethod");
            var expectedLocal = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(14, 13, 14, 26) // ACTUAL Span reported by test runner (local function line)
                                   .WithArguments("LocalFunction");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedOuter, expectedLocal);
        }

        [Test]
        public async Task MethodWithRecursiveImpureCall_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(int n)
    {
        if (n <= 0) return;
        Console.WriteLine(n); // Impure operation
        TestMethod(n - 1); // Recursive call
    }
}";
            // Expect diagnostic on TestMethod due to Console.WriteLine and recursive impure call
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 17, 10, 27) // ACTUAL Span reported by test runner (attribute line)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private void ImpureMethod()
    {
        Console.WriteLine(""Impure"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        ImpureMethod(); // Calling impure method
    }
}";
            // Expect diagnostic on TestMethod because it calls ImpureMethod
            var expectedOuter = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(15, 17, 15, 27) // CORRECTED Span of TestMethod (method identifier line)
                                   .WithArguments("TestMethod");
            var expectedImpure = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(9, 18, 9, 30) // CORRECTED Span of ImpureMethod (method identifier line)
                                   .WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedOuter, expectedImpure);
        }
    }
}


