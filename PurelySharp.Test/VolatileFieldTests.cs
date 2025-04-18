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
    public class VolatileFieldTests
    {
        [Test]
        public async Task ReadingVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int GetCounter()
    {
        return _counter; // Reading volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(14, 16, 14, 24).WithArguments("GetCounter"));
        }

        [Test]
        public async Task WritingVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public void IncrementCounter()
    {
        _counter++; // Writing volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(14, 9, 14, 17).WithArguments("IncrementCounter"));
        }

        [Test]
        public async Task RegularFieldAndVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _regularField;
    private volatile int _volatileField;

    [EnforcePure]
    public int CombineFields()
    {
        _regularField = 10; // This is already impure, but we're testing volatile field access
        return _regularField + _volatileField; // Reading volatile field should also make this impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 32, 16, 46).WithArguments("CombineFields"));
        }

        [Test]
        public async Task StaticVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private static volatile int _staticVolatileCounter;

    [EnforcePure]
    public int ReadStaticVolatileField()
    {
        return _staticVolatileCounter; // Reading static volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(14, 16, 14, 38).WithArguments("ReadStaticVolatileField"));
        }

        [Test]
        public async Task InterlockedWithVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading;



public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int IncrementAndGet()
    {
        // Using Interlocked with volatile field should be impure
        return Interlocked.Increment(ref _counter);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 42, 16, 50).WithArguments("IncrementAndGet"));
        }

        [Test]
        public async Task DoubleCheckedLockingPattern_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private volatile object _instance;
    private readonly object _lock = new object();

    [EnforcePure]
    [AllowSynchronization] // Even with AllowSynchronization, volatile read is impure
    public object GetSingletonInstance()
    {
        if (_instance == null) // First volatile read outside lock
        {
            lock (_lock)
            {
                if (_instance == null) // Second volatile read inside lock
                {
                    _instance = new object(); // Volatile write is impure
                }
            }
        }
        return _instance; // Final volatile read
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(19, 13, 19, 22).WithArguments("GetSingletonInstance"));
        }

        [Test]
        public async Task MultipleVolatileFields_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile bool _isInitialized;
    private volatile int _value;

    [EnforcePure]
    public void Initialize(int value)
    {
        _value = value; // First volatile field write
        _isInitialized = true; // Second volatile field write
    }

    [EnforcePure]
    public int GetValueIfInitialized()
    {
        if (_isInitialized) // Volatile read is impure
        {
            return _value; // Another volatile read is impure
        }
        return -1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Two diagnostics: one for Initialize method and one for GetValueIfInitialized
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(15, 9, 15, 15).WithArguments("Initialize"),
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(22, 13, 22, 27).WithArguments("GetValueIfInitialized"));
        }
    }
}