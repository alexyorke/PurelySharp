using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestFixture]
    public class StringOperationsTests
    {
        [Test]
        public async Task ComplexStringOperations_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StringInterpolation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
                .WithLocation(11, 19)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StringFormatting_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(int x, double y)
    {
        return string.Format(""X={0:D}, Y={1:F2}"", x, y);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}