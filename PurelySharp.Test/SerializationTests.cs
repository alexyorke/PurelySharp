using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using System.Text.Json; // Assuming System.Text.Json for tests
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class SerializationTests
    {
        private const string TestSetup = @"
#nullable enable
using System;
using System.Text.Json;
using PurelySharp.Attributes;

public class SimplePoco 
{ 
    public int Id { get; set; } 
    public string? Name { get; set; }
}
";

        [Test]
        public async Task PureMethodWithJsonSerializePoco_NoDiagnostic()
        {
            // Expectation limitation: Analyzer considers JsonSerializer.Serialize pure,
            // although it could theoretically be impure if property getters have side effects.
            var test = TestSetup + @"

public class TestClass
{
    [EnforcePure]
    public string TestMethod(SimplePoco poco)
    {
        // Serialization of simple POCOs is generally considered pure
        return JsonSerializer.Serialize(poco);
    }
}";
            // Analyzer needs to know JsonSerializer.Serialize is safe for simple types
            // or not flag external calls it doesn't recognize as impure by default.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithJsonDeserializePoco_Diagnostic()
        {
            var test = TestSetup + @"

public class TestClass
{
    [EnforcePure]
    public SimplePoco? TestMethod(string json)
    {
        // Deserialization should be flagged as impure
        return JsonSerializer.Deserialize<SimplePoco>(json);
    }
}";
            // Expect PS0002 diagnostic because Deserialize is impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(17, 24, 17, 34) // CORRECTED Span reported by test runner
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Added expected diagnostic
        }

        // TODO: Add tests for impure serialization/deserialization 
        // (e.g., types with impure getters/setters/constructors)
        // This would likely require analyzer enhancements to detect.
    }
}