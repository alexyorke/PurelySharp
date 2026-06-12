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
        public async Task UsingStatementExpressionResource_WithImpureDispose_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        using (new ImpureDisposable())
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

        [Test]
        public async Task UsingStatementExpressionCastToInterface_WithPureDispose_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using ((IDisposable)new PureDisposable())
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
        public async Task UsingDeclarationWithPureDisposable_NoDiagnostics()
        {
            var code = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var disposable = new PureDisposable(); // Empty Dispose body is accepted here.
    }
}

public class PureDisposable : IDisposable
{
    // Empty Dispose is accepted by the current using analysis.
    public void Dispose() { }
}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task UsingStatementWithPureDisposable_NoDiagnostics()
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
        // Using a local disposable with an empty Dispose body.
        using (var disposable = new PureDisposable())
        {
            // Some operation
        }
    }
}

public class PureDisposable : IDisposable
{
    // Empty Dispose is accepted by the current using analysis.
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
        public async Task UsingStatementExistingInterfaceLocal_WithPureDispose_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        IDisposable disposable = new PureDisposable();
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

        [Test]
        public async Task UsingStatementExistingInterfaceLocal_WithImpureDispose_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        IDisposable disposable = new ImpureDisposable();
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


