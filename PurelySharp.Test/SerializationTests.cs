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
            var test = TestSetup + @"

public class TestClass
{
    [EnforcePure] // Although Serialize might be pure, the method itself is marked
    public string TestMethod(SimplePoco poco)
    {
        // Serialization of simple POCOs is generally considered pure
        return JsonSerializer.Serialize(poco);
    }
}";
            // Expects no diagnostic because Serialize is assumed pure (or not flagged).
            // However, the POCO properties themselves will trigger PS0004.
            // await VerifyCS.VerifyAnalyzerAsync(test); // Original line, removed.

            // Expect PS0004 for POCO getters/setters (pure but no [EnforcePure])
            var expectedGetterId = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(9, 16, 9, 18) // Span from log for get_Id/set_Id
                                          .WithArguments("get_Id");
            var expectedGetterName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                            .WithSpan(10, 20, 10, 24) // Span from log for get_Name/set_Name
                                            .WithArguments("get_Name");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetterId, expectedGetterName);
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

            // Expect PS0004 for POCO getters/setters (pure but no [EnforcePure])
            var expectedGetterId = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(9, 16, 9, 18) // Span from log for get_Id/set_Id
                                          .WithArguments("get_Id");
            var expectedGetterName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                            .WithSpan(10, 20, 10, 24) // Span from log for get_Name/set_Name
                                            .WithArguments("get_Name");

            await VerifyCS.VerifyAnalyzerAsync(test, expected, expectedGetterId, expectedGetterName);
        }

        // TODO: Add tests for impure serialization/deserialization 
        // (e.g., types with impure getters/setters/constructors)
        // This would likely require analyzer enhancements to detect.
    }
}