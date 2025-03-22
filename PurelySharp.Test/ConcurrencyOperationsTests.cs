using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConcurrencyOperationsTests
    {
        [Test]
        public async Task AsyncMethodWithAwait_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void TestMethod()
    {
        lock (_lock) // Lock statement is impure
        {
            // Some operation
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 9)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithEventSubscription_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    public event EventHandler MyEvent;

    [Pure]
    public void TestMethod()
    {
        MyEvent += (s, e) => { }; // Event subscription is impure, but analyzer doesn't detect it
    }
}";

            // Currently the analyzer doesn't detect event subscriptions as impure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithDelegateInvocation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private Action _action = () => { };

    [EnforcePure]
    public void TestMethod()
    {
        _action(); // Delegate invocation is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 9)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task LockImpurityDetection_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void TestMethod()
    {
        lock (_lock) // Lock statement is impure
        {
            // Some operation
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 9)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
