using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ArrayMutationTests
    {
        [Test]
        public async Task ArrayReverseGeneric_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(int[] values)
    {
        Array.Reverse(values);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayReverseGenericRange_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(int[] values)
    {
        Array.Reverse(values, 0, values.Length);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
