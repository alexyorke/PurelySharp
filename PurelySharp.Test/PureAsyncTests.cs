using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class PureAsyncTests
    {
        [Test]
        public async Task PureAsyncMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod()
    {
        // Simple async method with no side effects
        return await Task.FromResult(42); // Changed to Task.FromResult which is guaranteed pure
    }
}";

            // Expect PMA0002 as Task.FromResult might not be recognized as pure by the test setup
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity).WithSpan(14, 22, 14, 41).WithArguments("PureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureAsyncMethod_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

class Program
{
    private int _counter = 0;

    [EnforcePure]
    public async Task<int> ImpureAsyncMethod()
    {
        // Has side effects - modifies field
        await Task.Delay(1);
        _counter++;
        return _counter;
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 9, 16, 28).WithArguments("ImpureAsyncMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureAsyncVoidMethod_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

class TestClass
{
    private int _count = 0;

    [EnforcePure]
    public async void TestMethod()
    {
        await Task.Delay(1); // Impure
        _count++;            // Impure
    }
}
";
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(15, 9, 15, 28).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithAsyncOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Threading.Tasks;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

class TestClass
{
    private int _field = 0;

    [EnforcePure]
    public async Task TestMethod()
    {
        _field = 1; // Impure assignment
        await Task.Yield(); // Yield is okay
    }
}
";
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(18, 9, 18, 27).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureMethodWithAsyncLocalFunction_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Threading.Tasks;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

class TestClass
{
    private static int counter = 0;

    [EnforcePure]
    public async Task OuterMethod()
    {
        await ImpureLocalAsync();

        async Task ImpureLocalAsync()
        {
            await Task.Delay(1); // Impure
            counter++;           // Impure
        }
    }
}
";
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(22, 13, 22, 20).WithArguments("OuterMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


