using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConcurrencyOperationsTests
    {
        [Test]
        public async Task AsyncMethodWithAwait_NoDiagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of 'lock' statements.
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        lock (_lock) // Lock statement is impure
        {
            // Some operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithEventSubscription_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of event subscriptions ('+=').
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    public event EventHandler MyEvent;

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        MyEvent += (s, e) => { }; // Event subscription is impure, but analyzer doesn't detect it
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithDelegateInvocation_Diagnostic()
        {
            var testCode = @"
using System;
using System.Threading;
using PurelySharp.Attributes;

public class TestClass
{
    private Action _impureAction = () => Console.WriteLine(); // Impure delegate target

    [EnforcePure]
    public void TestMethod()
    {
        // Invoking a delegate whose target might be impure
        _impureAction(); // Should trigger PS0002 if target analysis fails or is conservative
    }
}
";

            // Expect PS0002 because the delegate target's purity cannot be guaranteed by current analysis
            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(11, 17, 11, 27).WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Test]
        public async Task LockImpurityDetection_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of 'lock' statements.
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        lock (_lock) // Lock statement is impure -- Moved diagnostic to lock keyword
        {
            // Some operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithInterlockedIncrement_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of Interlocked operations.
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading;



public class TestClass
{
    private static int _counter;

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        Interlocked.Increment(ref _counter); // Impure atomic operation
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithInterlockedCompareExchange_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of Interlocked operations.
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Threading;



public class TestClass
{
    private static int _value;

    [EnforcePure]
    public void {|PS0002:TestMethod|}(int newValue, int comparand)
    {
        Interlocked.CompareExchange(ref _value, newValue, comparand); // Impure atomic operation
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Monitor Tests ---
        // TODO: Enable once analyzer recognizes Monitor.Enter/Exit as impure
        // Commented out Monitor test removed.

        // --- CancellationTokenSource Tests --- (MethodWithCTSCancel_Diagnostic is already commented out)
        // ...
        // --- TPL Dataflow Tests (Commented Out) ---
        // ...
    }
}


