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
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithEvent_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    public event EventHandler TestEvent;

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Invoking an event is impure
        TestEvent?.Invoke(this, EventArgs.Empty);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithEventSubscription_Diagnostic()
        {
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


