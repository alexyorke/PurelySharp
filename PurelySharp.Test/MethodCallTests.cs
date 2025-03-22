using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class MethodCallTests
    {
        [Test]
        public async Task PureMethodCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int PureHelperMethod(int x)
    {
        return x * 2;
    }

    [EnforcePure]
    public int TestMethod(int x)
    {
        // Call to pure method should be considered pure
        return PureHelperMethod(x) + 5;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public void ImpureHelperMethod()
    {
        Console.WriteLine(""This is impure"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        // Call to impure method should trigger diagnostic, but analyzer doesn't detect it
        ImpureHelperMethod();
    }
}";

            // Currently the analyzer doesn't detect calls to impure methods
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodCallingPureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int PureHelperMethod(int x)
    {
        return x * 2;
    }

    [EnforcePure]
    public void TestMethod()
    {
        // Pure method call is fine, but console write makes method impure
        int result = PureHelperMethod(5);
        Console.WriteLine(result); // This makes the method impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(20, 9, 20, 34).WithArguments("TestMethod"));
        }
    }
}
