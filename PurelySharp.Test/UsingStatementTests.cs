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

            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27)
                                 .WithArguments("TestMethod");


            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0002);
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

            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27)
                                 .WithArguments("TestMethod");


            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0002);
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

            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                    .WithSpan(17, 17, 17, 24)
                                    .WithArguments("Dispose");
            await VerifyCS.VerifyAnalyzerAsync(code, expectedPS0004);
        }

        [Test]
        public async Task UsingStatementWithPureDisposable_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO; // Restored System.IO, although not strictly needed for this specific test case

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Using a local pure disposable now
        using (var disposable = new PureDisposable()) // Correct usage
        {
            // Some operation
        }
    }
}

public class PureDisposable : IDisposable
{
    // Dispose is implicitly pure (empty body)
    public void Dispose() { }
}";








            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(22, 17, 22, 24)
                                        .WithArguments("Dispose");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0004);
        }
    }
}


