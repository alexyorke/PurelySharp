using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class ConcurrencyOperationsTests
    {
        [TestMethod]
        public async Task MethodWithLockStatement_Diagnostic()
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
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithEventSubscription_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public event EventHandler MyEvent;

    [EnforcePure]
    public void TestMethod()
    {
        MyEvent += (s, e) => { }; // Event subscription is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
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
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}