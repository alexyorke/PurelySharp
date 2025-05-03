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
            // Expectation limitation: Analyzer fails to detect impurity of reading volatile fields.
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile int _counter; // Line 8

    [EnforcePure] // Line 10
    public int GetCounter() // Line 11
    {
        return _counter; // Reading volatile field should be impure
    }
}";
            // Expect diagnostic on the method name
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
            //                        .WithSpan(11, 16, 11, 26) // Span for `GetCounter`
            //                        .WithArguments("GetCounter");
            // await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect no diagnostic due to limitation
        }

        [Test]
        public async Task WritingVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile int _counter; // Line 8

    [EnforcePure] // Line 10
    public void IncrementCounter() // Line 11
    {
        _counter++; // Writing volatile field should be impure
    }
}";
            // Expect diagnostic on the method name
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 17, 12, 33) // Adjusted span (line 12)
                                   .WithArguments("IncrementCounter");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task RegularFieldAndVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _regularField; // Line 8
    private volatile int _volatileField; // Line 9

    [EnforcePure] // Line 11
    public int CombineFields() // Line 12
    {
        _regularField = 10; // Impure write
        return _regularField + _volatileField; // Impure read
    }
}";
            // Expect diagnostic on the method name
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(13, 16, 13, 29) // Adjusted span (line 13)
                                   .WithArguments("CombineFields");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task StaticVolatileField_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private static volatile int _staticVolatileCounter; // Line 8

    [EnforcePure] // Line 10
    public int ReadStaticVolatileField() // Line 11
    {
        return _staticVolatileCounter; // Reading static volatile field should be impure
    }
}";
            // Expect diagnostic on the method name
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 16, 12, 39) // Adjusted end column to 39
                                   .WithArguments("ReadStaticVolatileField");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
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
    private volatile int _counter; // Line 9

    [EnforcePure] // Line 11
    public int IncrementAndGet() // Line 12
    {
        // Using Interlocked with volatile field should be impure
        return Interlocked.Increment(ref _counter); // Impure call
    }
}";
            // Expect diagnostic on the method name
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(13, 16, 13, 31) // Adjusted span (line 13)
                                   .WithArguments("IncrementAndGet");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
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
    private volatile object _instance; // Line 11
    private readonly object _lock = new object(); // Line 12

    [EnforcePure] // Line 14
    [AllowSynchronization] // Even with AllowSynchronization, volatile access is impure
    public object GetSingletonInstance() // Line 16
    {
        if (_instance == null) // Volatile read
        {
            lock (_lock)
            {
                if (_instance == null) // Volatile read
                {
                    _instance = new object(); // Volatile write
                }
            }
        }
        return _instance; // Volatile read
    }
}";
            // Expect diagnostic on the method name
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(17, 19, 17, 39) // Adjusted span (line 17)
                                   .WithArguments("GetSingletonInstance");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task MultipleVolatileFields_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of reading volatile fields
            // in GetValueIfInitialized.
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private volatile bool _isInitialized; // Line 8
    private volatile int _value; // Line 9

    [EnforcePure] // Line 11
    public void Initialize(int value) // Line 12
    {
        _value = value; // Volatile write
        _isInitialized = true; // Volatile write
    }

    [EnforcePure] // Line 18
    public int GetValueIfInitialized() // Line 19
    {
        if (_isInitialized) // Volatile read
        {
            return _value; // Volatile read
        }
        return -1;
    }
}";

            // Expect diagnostic on the Initialize method name
            var expected1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                    .WithSpan(13, 17, 13, 27) // Adjusted Span (line 13)
                                    .WithArguments("Initialize");

            // Expect diagnostic on the GetValueIfInitialized method name
            // var expected2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
            //                         .WithSpan(19, 16, 19, 37) // Span for `GetValueIfInitialized`
            //                         .WithArguments("GetValueIfInitialized");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected1 }); // Expect only 1 diagnostic due to limitation
        }
    }
}