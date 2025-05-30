using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Text;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StringOperationsTests
    {
        [Test]
        public async Task ComplexStringOperations_WithImpureSplit_Diagnostic()
        {
            // Expectation: This method should ideally be pure if string.Split and string.Join allocations are acceptable.
            // REVERTING: Analyzer currently flags this as PS0002.
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        // The impurity comes from Split
        var words = input.Split(' ')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.Trim().ToLower())
            .OrderBy(w => w.Length)
            .ThenBy(w => w);

        return string.Join("" "", words);
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(9, 19, 9, 29)
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
            // UPDATE: Expect 0 diagnostics after interpolation fix
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StringBuilderOperations_Diagnostic()
        {
            // Expectation limitation: Analyzer considers creating a new StringBuilder() instance impure.
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        var sb = new StringBuilder();
        sb.Append(input);
        return sb.ToString();
    }
}";
            // Expect diagnostic on the allocation, matching latest test framework output (method ID)
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(9, 19, 9, 29) // Updated span to method signature
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_ImpureFormat_Diagnostic()
        {
            // Expectation limitation: Analyzer considers string.Format impure.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x, double y)
    {
        return string.Format(""X = {0:D}, Y = {1:F2}"", x, y);
    }
}";
            // Restore original expectation: PS0002 on the containing method.
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(8, 19, 8, 29) // Original span for method signature
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
        sb.Append(""hello""); // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                  .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
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
        var sb = new StringBuilder();
        sb.Append(""hello"");
        return sb.ToString();
    }
}";
            // Expect diagnostic on the allocation, matching latest test framework output (method ID)
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(9, 19, 9, 29) // Updated span to method signature
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
        return sb.ToString();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithLocalStringBuilderToString_NoDiagnostic()
        {
            // Expectation: This method SHOULD be pure. Allocation of local SB is okay if only non-mutating methods are called.
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
        return sb.ToString();
    }
}";
            // UPDATED: Expect 0 diagnostics.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
