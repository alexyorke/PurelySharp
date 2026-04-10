using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StaticPropertyGetterTests
    {
        [Test]
        public async Task StaticPropertyWithImpureGetter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        private static int _counter;

        public static int Counter
        {
            get
            {
                Console.WriteLine(_counter);
                return ++_counter;
            }
        }

        [EnforcePure]
        public int {|PS0002:TestMethod|}()
        {
            return Counter;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StaticPropertyWithPureGetter_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        public static int Value
        {
            [EnforcePure]
            get
            {
                return 42;
            }
        }

        [EnforcePure]
        public int TestMethod()
        {
            return Value;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
