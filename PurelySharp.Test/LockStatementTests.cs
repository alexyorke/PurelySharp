using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LockStatementTests
    {
        // Test that a simple lock statement is considered impure by default
        [Test]
        public async Task LockStatement_ImpureByDefault()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void ImpureMethod()
    {
        lock (_lock)
        {
            Console.WriteLine(""Inside lock"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(16, 9, 16, 13).WithArguments("ImpureMethod"));
        }

        // Test that a lock statement with pure operations should be pure when option enabled
        // Note: This test is marked Explicit because the feature is in progress
        [Test]
        public async Task LockStatement_WithPureOperations_ShouldBePure()
        {
            var test = @"
using System;
using System.Diagnostics;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();
    private readonly int _value = 42;

    [EnforcePure]
    [AllowSynchronization]
    public int PureMethodWithLock()
    {
        int result;
        lock (_lock)
        {
            result = _value;
        }
        return result;
    }
}";

            // This test is expected to pass once the implementation is complete
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Test that verifies the current behavior for lock statements on readonly objects with pure operations
        [Test]
        public async Task LockStatement_WithPureOperations_CurrentBehavior()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private readonly int[] _array = new int[10];

    [EnforcePure]
    [AllowSynchronization]
    public int PureMethodWithLock()
    {
        lock (_lock)
        {
            return _array[0]; // Pure operation - just reading
        }
    }
}";

            // With the updated implementation, no diagnostic is expected
            // because the lock statement is on a readonly object, the method has the AllowSynchronization attribute,
            // and all operations inside the lock are pure.
            await VerifyCS.VerifyAnalyzerAsync(test, new DiagnosticResult[0]);
        }

        // Test that a lock statement with impure operations is reported as impure
        [Test]
        public async Task LockStatement_WithImpureOperations_IsImpure()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private int _value = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void ImpureMethodWithLock()
    {
        lock (_lock)
        {
            _value++; // This is impure because it modifies state
        }
    }
}";
            var expected = VerifyCS.Diagnostic().WithSpan(20, 13, 20, 21).WithArguments("ImpureMethodWithLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // Test that a lock with a non-readonly object is considered impure
        [Test]
        public async Task LockStatement_NonReadonlyObject_IsImpure()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private object _nonReadonlyLock = new object(); // Non-readonly lock object
    private int _counter = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void ImpureMethodWithNonReadonlyLock()
    {
        lock (_nonReadonlyLock)
        {
            _counter++; // This is the impure operation
        }
    }
}";

            // The diagnostic is for the impure method itself, not the field increment
            var expected = VerifyCS.Diagnostic().WithSpan(18, 9, 18, 13).WithArguments("ImpureMethodWithNonReadonlyLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


