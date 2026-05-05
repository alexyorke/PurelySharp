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
        public async Task UsingDeclarationWithPureDisposable_MissingAttributeDiagnostic()
        {
            var code = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var disposable = new PureDisposable(); // Pure disposable body, but Dispose is still unannotated.
    }
}

public class PureDisposable : IDisposable
{
    // Dispose has an empty body, but it still expects PS0004 because it is unannotated.
    public void Dispose() { }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task UsingStatementWithPureDisposable_MissingAttributeDiagnostic()
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
        // Using a local disposable with an unannotated but empty Dispose.
        using (var disposable = new PureDisposable()) // Correct usage
        {
            // Some operation
        }
    }
}

public class PureDisposable : IDisposable
{
    // Dispose has an empty body, but it still expects PS0004 because it is unannotated.
    public void Dispose() { }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UsingDeclarationLocalReference_DoesNotFlagResourceAsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        var disposable = new PureDisposable();
        using (disposable)
        {
        }
    }
}

public class PureDisposable : IDisposable
{
    public void Dispose() { }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UsingStatementExistingLocal_WithImpureDispose_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        var disposable = new ImpureDisposable();
        using (disposable)
        {
        }
    }
}

public class ImpureDisposable : IDisposable
{
    private int _disposeCount;

    public void Dispose()
    {
        _disposeCount++;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


