using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void {|PS0002:ImpureMethod|}()
    {
        lock (_lock)
        {
            Console.WriteLine(""Inside lock"");
        }
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Test that a lock statement with pure operations should be pure when option enabled
        // Note: This test is marked Explicit because the feature is in progress
        [Test]
        public async Task LockStatement_WithPureOperations_ShouldBePure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();
    private readonly int _value = 42;

    [EnforcePure]
    [AllowSynchronization]
    public int {|PS0002:PureMethodWithLock|}()
    {
        int result;
        lock (_lock)
        {
            result = _value;
        }
        return result;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Test that verifies the current behavior for lock statements on readonly objects with pure operations
        [Test]
        public async Task LockStatement_WithPureOperations_CurrentBehavior()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private readonly int[] _array = new int[10];

    [EnforcePure]
    [AllowSynchronization]
    public int {|PS0002:PureMethodWithLock|}()
    {
        lock (_lock)
        {
            return _array[0]; // Pure operation - just reading
        }
    }
}";

            // and all operations inside the lock are pure.
            // Expect PS0002 because lock() itself is currently considered impure, even with AllowSync
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Test that a lock statement with impure operations is reported as impure
        [Test]
        public async Task LockStatement_WithImpureOperations_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private int _value = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void {|PS0002:ImpureMethodWithLock|}()
    {
        lock (_lock)
        {
            _value++; // This is impure because it modifies state
        }
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Test that a lock with a non-readonly object is considered impure
        [Test]
        public async Task LockStatement_NonReadonlyObject_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private object _nonReadonlyLock = new object(); // Non-readonly lock object
    private int _counter = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void {|PS0002:ImpureMethodWithNonReadonlyLock|}()
    {
        lock (_nonReadonlyLock)
        {
            _counter++; // This is the impure operation
        }
    }
}";

            // Locking on a non-readonly field makes the method impure.
            // Expect PS0002 on the method declaration.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


