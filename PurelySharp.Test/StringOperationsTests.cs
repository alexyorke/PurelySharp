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
        public async Task ComplexStringOperations_WithSplit_NoDiagnostic()
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
        // Split and the subsequent string/LINQ pipeline are pure in the current analyzer model.
        var words = input.Split(' ')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.Trim().ToLower())
            .OrderBy(w => w.Length)
            .ThenBy(w => w);

        return string.Join("" "", words);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string TestMethod(string input)
    {
        var sb = new StringBuilder();
        sb.Append(input);
        return sb.ToString();
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(9, 19, 9, 29)
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_ImpureFormat_Diagnostic()
        {

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

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(8, 19, 8, 29)
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_OneArgument_ImpureFormat_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x)
    {
        return string.Format(""{0:D}"", x);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(8, 19, 8, 29)
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_ThreeArgument_ImpureFormat_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x, int y, int z)
    {
        return string.Format(""{0} {1} {2}"", x, y, z);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(8, 19, 8, 29)
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
        sb.Append(""hello"");
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                  .WithSpan(9, 17, 9, 27)
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

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(9, 19, 9, 29)
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
