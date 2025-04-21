using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
    public int {|PS0002:GetCounter|}()
    {
        return _counter; // Reading volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:IncrementCounter|}()
    {
        _counter++; // Writing volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int {|PS0002:CombineFields|}()
    {
        _regularField = 10; // This is already impure, but we're testing volatile field access
        return _regularField + _volatileField; // Reading volatile field should also make this impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int {|PS0002:ReadStaticVolatileField|}()
    {
        return _staticVolatileCounter; // Reading static volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int {|PS0002:IncrementAndGet|}()
    {
        // Using Interlocked with volatile field should be impure
        return Interlocked.Increment(ref _counter);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public object {|PS0002:GetSingletonInstance|}()
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

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:Initialize|}(int value)
    {
        _value = value; // First volatile field write
        _isInitialized = true; // Second volatile field write
    }

    [EnforcePure]
    public int {|PS0002:GetValueIfInitialized|}()
    {
        if (_isInitialized) // Volatile read is impure
        {
            return _value; // Another volatile read is impure
        }
        return -1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}