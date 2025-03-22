using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UsingStatementTests
    {
        [Test]
        public async Task UsingStatement_WithoutDisposable_IsPure()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using (var file = File.OpenRead(""test.txt"")) // Using statement is impure
        {
            // Some operation
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(13, 27)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task UsingDeclaration_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var file = File.OpenRead(""test.txt""); // Using declaration is impure
        // Some operation
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(13, 26)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task UsingDeclarationWithPureDisposable_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class PureDisposable : IDisposable
{
    public void Dispose() { } // Empty dispose method is pure
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using var disposable = new PureDisposable(); // Pure disposable is ok
        // Some operation
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}