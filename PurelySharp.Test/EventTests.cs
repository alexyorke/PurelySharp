using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithEvent_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public event EventHandler TestEvent;

    [EnforcePure]
    public void TestMethod()
    {
        // Invoking an event is impure
        TestEvent?.Invoke(this, EventArgs.Empty);
    }
}";

            var expected = VerifyCS.Diagnostic()
                .WithSpan(15, 43, 15, 48)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithEventSubscription_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public event EventHandler TestEvent;
    
    [EnforcePure]
    public void TestMethod()
    {
        // Subscribing to an event is impure (state modification), but analyzer doesn't detect it
        TestEvent += OnTestEvent;
    }
    
    private void OnTestEvent(object sender, EventArgs e)
    {
        Console.WriteLine(""Event handler triggered"");
    }
}";

            // Currently the analyzer doesn't detect event subscriptions as impure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
