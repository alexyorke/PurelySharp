using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.IO;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UsingStatementTests
    {
        [Test]
        public async Task UsingStatement_WithImpureDisposable_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using (var file = File.OpenRead(""test.txt""))
        {
            // Some operation
        }
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Pass expected diagnostic
        }

        [Test]
        public async Task UsingDeclaration_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var file = File.OpenRead(""test.txt"");
        // Some operation
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Pass expected diagnostic
        }

        [Test]
        public async Task UsingDeclarationWithPureDisposable_NoDiagnostic()
        {
            var code = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var disposable = new PureDisposable(); // Pure disposable, Dispose is pure
    }
}

public class PureDisposable : IDisposable
{
    // Dispose is implicitly pure (empty body)
    public void Dispose() { }
}";
            // Expect no diagnostic because the resource and Dispose() are pure.
            await VerifyCS.VerifyAnalyzerAsync(code);
        }
    }
}


