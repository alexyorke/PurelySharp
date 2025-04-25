using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

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
    public void {|PS0002:TestMethod|}()
    {
        using (var file = File.OpenRead(""test.txt""))
        {
            // Some operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:TestMethod|}()
    {
        using var file = File.OpenRead(""test.txt"");
        // Some operation
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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


