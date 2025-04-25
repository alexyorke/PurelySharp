using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RecordTests
    {
        // Minimal attribute definition reused by the test cases
        private const string MinimalEnforcePureAttributeSource = """
namespace PurelySharp.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Property | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
    public sealed class EnforcePureAttribute : System.Attribute { }
}
""";

        [Test]
        public async Task ImmutableRecord_NoDiagnostic()
        {
            var isExternalInit = """
namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
""";

            var testCode = """
// Requires C# 9+ and IsExternalInit polyfill
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

// No CS0518 expected here due to polyfill
public record Person(string Name, int Age);

public class TestClass
{
    [EnforcePure]
    public string GetPersonInfo(Person person)
    {
        // Accessing properties of an immutable record should be pure
        return $"{ person.Name} is { person.Age } years old";
    }
}
""";

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, isExternalInit, MinimalEnforcePureAttributeSource }
                }
            };

            await verifierTest.RunAsync();
        }

        [Test]
        public async Task RecordWithPureMethod_NoDiagnostic()
        {
            var test = """
// Requires C# 9+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

""" + MinimalEnforcePureAttributeSource + """
public record Calculator
{
    [EnforcePure]
    public int Add(int x, int y) => x + y; // Add is pure
}

public class TestClass
{
    [EnforcePure]
    // UseCalculator calls a pure method, so it should be considered pure by the analyzer
    public int UseCalculator(Calculator calc, int a, int b)
    {
        return calc.Add(a, b);
    }
}
""";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MutableRecord_ShouldProduceDiagnostic()
        {
            var test = """
// Requires C# 9+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

""" + MinimalEnforcePureAttributeSource + """
public record MutablePerson
{
    // CS8618 is on the property name
    public string {|CS8618:Name|} { get; set; }
    public int Age { get; set; }
}

public class TestClass
{
    [EnforcePure]
    // PS0002 because it modifies state
    public void {|PS0002:UpdatePerson|}(MutablePerson person)
    {
        person.Name = "John"; // Impure assignment
    }
}
""";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RecordWithMixedProperties_ShouldProduceDiagnostic()
        {
            var isExternalInit = """
namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
""";

            var testCode = """
// Requires C# 9+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

public record Person
{
    // CS8618 is on the property name, CS0518 is implicitly handled by polyfill
    public string {|CS8618:Name|} { get; init; }
    private int age;
    public int Age
    {
        get => age;
        set => age = value; // Impure setter
    }
}

public class TestClass
{
    [EnforcePure]
    // PS0002 because it calls an impure setter
    public void {|PS0002:UpdateAge|}(Person person)
    {
        person.Age = 30;
    }
}
""";

            var verifierTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, isExternalInit, MinimalEnforcePureAttributeSource }
                }
            };

            await verifierTest.RunAsync();
        }
    }
}
