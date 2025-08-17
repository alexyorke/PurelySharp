using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullConditionalTests
    {
        [Test]
        public async Task PureMethodWithNullConditional_NoDiagnostic()
        {

            var test = """
using System;
using PurelySharp.Attributes;

public class TestClass
{
    public string Value { get; set; }

    [EnforcePure]
    public int? TestMethod(TestClass obj)
    {
        // Pure: Null conditional access to a property length
        return obj?.Value?.Length;
    }
}
""";



            var expectedGet = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(6, 19, 6, 24).WithArguments("get_Value");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedGet);
        }

        [Test]
        public async Task PureMethodWithNullConditionalAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _field;

    [EnforcePure]
    public string {|PS0002:TestMethod|}(TestClass obj)
    {
        // Null conditional is pure, but field increment is impure
        var result = obj?.ToString() ?? ""null"";
        _field++;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


