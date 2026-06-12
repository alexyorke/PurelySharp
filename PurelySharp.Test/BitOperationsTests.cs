using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BitOperationsTests
    {
        [Test]
        public async Task BitOperationsDeterministicHelpers_NoDiagnostic()
        {
            var test = @"
using System.Numerics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(uint value, ulong wider)
    {
        return BitOperations.LeadingZeroCount(value) + BitOperations.PopCount(wider);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task BitOperationsWithImpureArgument_Diagnostic()
        {
            var test = @"
using System;
using System.Numerics;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return BitOperations.PopCount((ulong)Console.Read());
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
