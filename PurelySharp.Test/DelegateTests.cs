using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DelegateTests
    {
        [Test]
        public async Task PureMethodWithDelegate_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void /*|PS0002:*/TestMethod/*|*/()
    {
        // Creating a delegate but not invoking it
        // The analyzer currently considers creating a delegate with an impure
        // target (Console.WriteLine) to be impure itself
        Action action = () => Console.WriteLine(""Hello"");
        
        // The method doesn't invoke the delegate, but it's still marked impure
        // due to the lambda's body containing an impure operation
        action(); // Invoking might be impure, but defining might be okay
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(10, 29, 10, 39).WithArguments("TestMethod"));
        }

        [Test]
        public async Task ImpureMethodWithDelegate_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Creating a delegate directly in an impure method
        Action action = () => Console.WriteLine(""Hello"");
        
        // Invoking the delegate makes the method impure
        action();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithDelegateInvocation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private readonly Action _action;
    
    public TestClass()
    {
        _action = () => Console.WriteLine(""Hello from field delegate"");
    }

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        // Invoking a delegate stored in a field
        _action();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


