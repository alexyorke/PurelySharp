using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Text;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StringOperationsTests
    {
        [Test]
        public async Task ComplexStringOperations_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        var words = input.Split(' ')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.Trim().ToLower())
            .OrderBy(w => w.Length)
            .ThenBy(w => w);

        return string.Join("" "", words);
    }
}";

            // Expect PMA0002 because string.Split is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(19, 16, 19, 39) // Span of input.Split(' ')
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringInterpolation_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x, string y)
    {
        return $""Value: {x}, Text: {y.ToUpper()}"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StringBuilderOperations_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;



public class TestClass
{
    [EnforcePure]
    public string TestMethod(string[] inputs)
    {
        var sb = new StringBuilder();
        foreach (var input in inputs)
        {
            sb.Append(input); // StringBuilder operations are impure
        }
        return sb.ToString();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(16, 13)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x, double y)
    {
        return string.Format(""X={0:D}, Y={1:F2}"", x, y);
    }
}";

            // Expect PMA0002 because string.Format is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(12, 16, 12, 56) // Span of string.Format(...)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithStringBuilderAppend_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(StringBuilder sb)
    {
        sb.Append(""hello""); // Modifying StringBuilder parameter is impure
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(13, 9, 13, 27)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithStringBuilderAppend_OnLocal_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(""hello""); // Modifying local StringBuilder is impure
        return sb.ToString();
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(14, 9, 14, 27)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithStringBuilderToString_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;



public class TestClass
{
    [EnforcePure]
    public string TestMethod(StringBuilder sb)
    {
        return sb.ToString(); // ToString() is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithLocalStringBuilderToString_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        StringBuilder sb = new StringBuilder(""initial"");
        return sb.ToString(); // ToString() is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


