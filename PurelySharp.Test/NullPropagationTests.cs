using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullPropagationTests
    {
        [Test]
        public async Task PureMethodWithNullPropagation_NoDiagnostic_AnalyzerMismatch()
        {

            var test = """
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = "";
    public int    Age  { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public string TestMethod(Person? person)
    {
        // Null-propagation itself is pure; analyzer flags this due to setter on Name.
        return person?.Name ?? "Unknown";
    }
}
""";









            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 23).WithArguments("get_Name");
            var expectedGetAge = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 19, 8, 22).WithArguments("get_Age");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetName, expectedGetAge);
        }

        [Test]
        public async Task ImpureMethodWithNullPropagation_Diagnostic()
        {
            var test = """
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = "";
    public int    Age  { get; set; }

    public void LogToConsole() => Console.WriteLine(Name);
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Person? person)
    {
        // Null-propagation followed by an impure operation.
        person?.LogToConsole();
    }
}
""";


            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                       .WithSpan(16, 17, 16, 27)
                       .WithArguments("TestMethod");
            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 23).WithArguments("get_Name");
            var expectedGetAge = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 19, 8, 22).WithArguments("get_Age");

            await VerifyCS.VerifyAnalyzerAsync(test,
                expectedTestMethod,
                expectedGetName,
                expectedGetAge
                );
        }

        [Test]
        public async Task PureMethodWithNullPropagationAndImpureOperation_Diagnostic()
        {
            var test = """
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = "";
    public int    Age  { get; set; }
}

public class TestClass
{
    private int _counter;

    [EnforcePure]
    public string TestMethod(Person? person)
    {
        // Pure null-propagation.
        var name = person?.Name ?? "Unknown";

        // Impure state modification.
        _counter++;

        return name;
    }
}
""";


            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 19, 7, 23).WithArguments("get_Name");
            var expectedGetAge = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 19, 8, 22).WithArguments("get_Age");
            var originalExpected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                       .WithSpan(16, 19, 16, 29)
                       .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, originalExpected, expectedGetName, expectedGetAge);
        }
    }
}
