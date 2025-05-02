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
        public string {|PS0002:CallDefaultToString|}() // Line 16 - Added inline diagnostic markup
        {
            var instance = new MySimpleClass { Value = 42 };
            // Calling the default object.ToString() implementation
            return instance.ToString(); // Line 20
        }
    }
}";

            // Pass only the test string; expectations are inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // TODO: Add a test for an explicitly overridden PURE ToString()
        // TODO: Add a test for an explicitly overridden IMPURE ToString()
    }
}