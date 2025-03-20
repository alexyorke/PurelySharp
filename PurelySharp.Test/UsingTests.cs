using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UsingTests
    {
        [Test]
        public async Task PureMethodWithUsing_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class PureDisposable : IDisposable
{
    public void Dispose() { } // Empty dispose method is pure
}

public class TestClass
{
    [Pure]
    public void TestMethod()
    {
        using (var disposable = new PureDisposable()) // Pure using statement
        {
            // No impure operations
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithUsing_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public void TestMethod()
    {
        using (var file = File.OpenRead(""test.txt"")) // Using with impure operation
        {
            // Some operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(13, 27, 13, 52).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithUsingAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class PureDisposable : IDisposable
{
    public void Dispose() { } // Empty dispose method is pure
}

public class TestClass
{
    [Pure]
    public void TestMethod()
    {
        using (var disposable = new PureDisposable())
        {
            Console.WriteLine(""Inside using""); // Impure operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(20, 13, 20, 46).WithArguments("TestMethod"));
        }
    }
}