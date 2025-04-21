using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Text;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StringOperationsTests
    {
        [Test]
        public async Task ComplexStringOperations_WithImpureSplit_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(string input)
    {
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
    public string {|PS0002:TestMethod|}(int x, string y)
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
    public string {|PS0002:TestMethod|}(string[] inputs)
    {
        var sb = new StringBuilder();
        foreach (var input in inputs)
        {
            sb.Append(input);
        }
        return sb.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string {|PS0002:TestMethod|}(int x, double y)
    {
        return string.Format(""X={0:D}, Y={1:F2}"", x, y);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:TestMethod|}(StringBuilder sb)
    {
        sb.Append(""hello"");
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string {|PS0002:TestMethod|}()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(""hello"");
        return sb.ToString();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string {|PS0002:TestMethod|}(StringBuilder sb)
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
    public string {|PS0002:TestMethod|}()
    {
        StringBuilder sb = new StringBuilder(""initial"");
        return sb.ToString();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


