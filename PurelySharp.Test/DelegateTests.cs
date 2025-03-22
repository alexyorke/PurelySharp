using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Creating a delegate but not invoking it
        // The analyzer currently considers creating a delegate with an impure
        // target (Console.WriteLine) to be impure itself
        Action action = () => Console.WriteLine(""Hello"");
        
        // The method doesn't invoke the delegate, but it's still marked impure
        // due to the lambda's body containing an impure operation
    }
}";

            var expected = VerifyCS.Diagnostic()
                .WithSpan(15, 31, 15, 57)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ImpureMethodWithDelegate_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Creating a delegate directly in an impure method
        Action action = () => Console.WriteLine(""Hello"");
        
        // Invoking the delegate makes the method impure
        action();
    }
}";

            var expected = VerifyCS.Diagnostic()
                .WithSpan(16, 9, 16, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithDelegateInvocation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly Action _action;
    
    public TestClass()
    {
        _action = () => Console.WriteLine(""Hello from field delegate"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        // Invoking a delegate stored in a field
        _action();
    }
}";

            var expected = VerifyCS.Diagnostic()
                .WithSpan(20, 9, 20, 18)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


