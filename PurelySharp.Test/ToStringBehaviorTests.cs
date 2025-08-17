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


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(16, 23, 16, 42)
                                   .WithArguments("CallDefaultToString");


            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(10, 20, 10, 25)
                                        .WithArguments("get_Value");




            await VerifyCS.VerifyAnalyzerAsync(test, expected, expectedGetter);
        }



    }
}