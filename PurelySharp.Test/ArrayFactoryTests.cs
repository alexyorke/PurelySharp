using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ArrayFactoryTests
    {
        [Test]
        public async Task ArrayEmptyReturned_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] TestMethod()
    {
        return Array.Empty<int>();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
