using NUnit.Framework;
using System;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using Microsoft.CodeAnalysis.Testing;

namespace PurelySharp.Test
{
    [TestFixture]
    public class WhileLoopTests
    {

        private const string MinimalEnforcePureAttributeSource = @"
namespace PurelySharp.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class EnforcePureAttribute : System.Attribute { }
}
";

        [Test]
        public async Task PureWhileLoop_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public int PureMethod(int limit)
        {
            int i = 0;
            int sum = 0;
            while (i < limit) // Pure condition
            {
                sum += i; // Pure body
                i++;      // Pure body
            }
            return sum;
        }
    }
}
" + MinimalEnforcePureAttributeSource;

            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Test]
        public async Task ImpureConditionInWhileLoop_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        private int _counter = 0;

        // Marked with EnforcePure but is impure due to field modification
        [EnforcePure]
        private bool IsConditionMet()
        {
            _counter++; // Impure operation
            return _counter < 5;
        }

        // Marked with EnforcePure, calls impure method in loop condition
        [EnforcePure]
        public void TestMethod()
        {
            while (IsConditionMet()) // Impure call in condition
            {
                // Loop body doesn't matter if condition is impure
            }
        }
    }
}
" + MinimalEnforcePureAttributeSource;


            var expected = new[]
            {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(13, 22, 13, 36).WithArguments("IsConditionMet"),

            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        [Test]
        public async Task ImpureBodyInWhileLoop_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        private int _state = 0; // Mutable state

        [EnforcePure]
        public void TestMethod(int limit)
        {
            int i = 0;
            while (i < limit) // Pure condition
            {
                _state += i; // Impure operation in body
                i++;
            }
        }
    }
}
" + MinimalEnforcePureAttributeSource;


            var expected = new[] {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(12, 21, 12, 31)
                                   .WithArguments("TestMethod"),
             };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}