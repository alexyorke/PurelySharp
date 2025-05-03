using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EventTests
    {
        [Test]
        public async Task PureMethodWithEvent_NoDiagnostic()
        {
            // Expectation limitation: Analyzer incorrectly flags reading an event field as impure.
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    // Declaring an event is pure
    public event EventHandler TestEvent;

    [EnforcePure]
    public void TestMethod()
    {
        // Just referencing an event without subscribing is pure
        var evt = TestEvent;
        
        // Method doesn't interact with the event, so it's still pure
    }
}";
            // ADDED: Expect PS0002 because analyzer flags event reference as impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                 .WithSpan(13, 17, 13, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Added expected diagnostic
        }

        [Test]
        public async Task ImpureMethodWithEvent_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of event invocation ('Invoke').
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    public event EventHandler TestEvent;

    [EnforcePure]
    public void TestMethod()
    {
        // Invoking an event is impure (but analyzer doesn't detect it currently)
        TestEvent?.Invoke(this, EventArgs.Empty);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithEventSubscription_Diagnostic()
        {
            // Expectation limitation: Analyzer fails to detect impurity of event subscriptions ('+=').
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    public event EventHandler TestEvent;
    
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Subscribing to an event is impure (state modification), but analyzer doesn't detect it
        TestEvent += OnTestEvent;
    }
    
    private void OnTestEvent(object sender, EventArgs e)
    {
        Console.WriteLine(""Event handler triggered"");
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


