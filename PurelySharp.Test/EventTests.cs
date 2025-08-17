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

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                 .WithSpan(13, 17, 13, 27)
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureMethodWithEvent_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    public event Action MyEvent;

    [EnforcePure]
    public void TestMethod()
    {
        // Event subscription modifies state and should be impure.
        MyEvent += () => Console.WriteLine(); // Expect PS0002 if no specific rule flags this
    }
}
";



            var expectedDiagnostic = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(10, 17, 10, 27).WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Test]
        public async Task PureMethodWithEventSubscription_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class EventSource
{
    public event EventHandler TestEvent;
    // Removed [EnforcePure] - Base methods shouldn't usually need it unless explicitly designed pure
    protected virtual void OnTestEvent(object sender, EventArgs e) => TestEvent?.Invoke(this, e); // Added parameters
}

public class TestClass : EventSource
{
    [EnforcePure] // Impure: Event subscription modifies state
    public void TestMethod()
    {
        this.TestEvent += OnTestEvent;
    }

    [EnforcePure] // Impure: Console.WriteLine
    protected override void OnTestEvent(object sender, EventArgs e) // Added parameters
    {
        Console.WriteLine(""Event handled"");
    }
}";


            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(15, 17, 15, 27).WithArguments("TestMethod");
            var expectedOnTestEventOverride = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(21, 29, 21, 40).WithArguments("OnTestEvent");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedTestMethod, expectedOnTestEventOverride });
        }
    }
}


