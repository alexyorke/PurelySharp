using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ToStringBehaviorTests
    {
        // Define a simple class that does NOT override ToString()
        public class MySimpleClass
        {
            public int Value { get; set; }
        }

        [Test]
        public async Task DefaultToStringCall_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace PurelySharp.Test // Add namespace to match outer scope
{
    // Re-define class inside the test string scope
    public class MySimpleClass
    {
        public int Value { get; set; }
    }

    public class TestClass
    {
        [EnforcePure]
        public string CallDefaultToString() // Line 16 - REMOVED inline diagnostic markup
        {
            var instance = new MySimpleClass { Value = 42 };
            // Calling the default object.ToString() implementation
            return instance.ToString(); // Line 20
        }
    }
}";

            // Expect diagnostic because default object.ToString() uses GetType() (reflection).
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(16, 23, 16, 42) // Corrected span based on test output
                                   .WithArguments("CallDefaultToString");

            // Expect PS0004 for getter and setter as they are pure but lack the attribute
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(10, 20, 10, 25) // Span from log for get_Value/set_Value
                                        .WithArguments("get_Value");
            var expectedSetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(10, 20, 10, 25) // Span from log for get_Value/set_Value
                                        .WithArguments("set_Value");

            await VerifyCS.VerifyAnalyzerAsync(test, expected, expectedGetter, expectedSetter); // Pass expected diagnostics
        }

        // TODO: Add a test for an explicitly overridden PURE ToString()
        // TODO: Add a test for an explicitly overridden IMPURE ToString()
    }
}