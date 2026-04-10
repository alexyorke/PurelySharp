using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InstancePropertyGetterTests
    {
        [Test]
        public async Task InstancePropertyWithImpureGetter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        private int _counter;

        public int Counter
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
        public async Task InParameterPropertyWithImpureGetter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public readonly struct CounterStruct
    {
        private readonly int _value;

        [EnforcePure]
        public CounterStruct(int value)
        {
            _value = value;
        }

        public int Value
        {
            get
            {
                Console.WriteLine(_value);
                return _value;
            }
        }
    }

    public class TestClass
    {
        [EnforcePure]
        public int {|PS0002:TestMethod|}(in CounterStruct counter)
        {
            return counter.Value;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMarkedGetterWithImpureBody_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class Data
    {
        public int Value
        {
            [Pure]
            get
            {
                Console.WriteLine(1);
                return 1;
            }
        }
    }

    public class TestClass
    {
        [EnforcePure]
        public int {|PS0002:TestMethod|}(Data data)
        {
            return data.Value;
        }
    }
}";

            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                .WithSpan(9, 20, 9, 25)
                .WithArguments("get_Value");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter);
        }
    }
}
