using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullComparisonTests
    {
        [Test]
        public async Task PureMethodWithNullComparison_NoDiagnostic()
        {
            var test = @"
using System;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(object obj)
    {
        // Null comparison is considered pure
        return obj == null;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullComparison_Diagnostic()
        {
            var test = @"
using System;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(object obj)
    {
        // Null comparison with console write is impure
        if (obj == null)
        {
            Console.WriteLine(""Object is null"");
        }
    }
}";

            // The analyzer detects the Console.WriteLine as impure
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(15, 13, 15, 48).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithNullComparisonAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;

public class TestClass
{
    private int _field;

    [EnforcePure]
    public bool TestMethod(object obj)
    {
        // Null comparison is pure, but field increment is impure
        bool isNull = obj == null;
        _field++;
        return isNull;
    }
}";

            // The analyzer detects the field modification as impure
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 9, 16, 17).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


