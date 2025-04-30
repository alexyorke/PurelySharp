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
        public async Task PureMethodWithJsonDeserializePoco_NoDiagnostic()
        {
            var test = TestSetup + @"

public class TestClass
{
    [EnforcePure]
    public SimplePoco? TestMethod(string json)
    {
        // Deserialization to simple POCOs (assuming no side effects in ctor/setters) is pure
        return JsonSerializer.Deserialize<SimplePoco>(json);
    }
}";
            // Similar to Serialize, depends on analyzer's handling of unknown external calls.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // TODO: Add tests for impure serialization/deserialization 
        // (e.g., types with impure getters/setters/constructors)
        // This would likely require analyzer enhancements to detect.
    }
}