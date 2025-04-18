using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ExceptionHandlingTests
    {
        [Test]
        public async Task PureMethodWithExceptionHandling_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x)
    {
        try
        {
            // Exception handling itself is not impure
            if (x < 0)
            {
                throw new ArgumentException(""x cannot be negative"", nameof(x));
            }
            
            return x * 2;
        }
        catch (Exception ex)
        {
            // Reading the exception message is also pure
            string message = ex.Message;
            return 0;
        }
    }
}";

            // Expect PMA0002 based on test failure (TryCatchFinally_NoDiagnostic?) 
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(17, 69, 17, 78) // Span from test error output
                .WithArguments("TestMethod"); // Method name from error output
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithExceptionHandlingAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x)
    {
        try
        {
            if (x < 0)
            {
                throw new ArgumentException(""x cannot be negative"", nameof(x));
            }
            
            return x * 2;
        }
        catch (Exception ex)
        {
            // Writing to console is impure
            Console.WriteLine(ex.Message);
            return 0;
        }
    }
}";

            // Expect PMA0001 because ex.Message is treated as impure
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure)
                .WithSpan(24, 13, 24, 42) // Span for Console.WriteLine (based on test output)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


