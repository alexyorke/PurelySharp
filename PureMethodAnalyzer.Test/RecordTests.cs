using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace PureMethodAnalyzer.Test
{
    [TestFixture]
    public class RecordTests
    {
        [Test]
        public async Task ImmutableRecord_NoDiagnostic()
        {
            var test = @"
using System;
using System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record Person(string Name, int Age);

public class TestClass
{
    [EnforcePure]
    public string GetPersonInfo(Person person)
    {
        return $""{person.Name} is {person.Age} years old"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("CS0518")
                    .WithSpan(8, 29, 8, 33)
                    .WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                DiagnosticResult.CompilerError("CS0518")
                    .WithSpan(8, 39, 8, 42)
                    .WithArguments("System.Runtime.CompilerServices.IsExternalInit"));
        }

        [Test]
        public async Task RecordWithPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record Calculator
{
    [EnforcePure]
    public int Add(int x, int y) => x + y;
}

public class TestClass
{
    [EnforcePure]
    public int UseCalculator(Calculator calc, int a, int b)
    {
        return calc.Add(a, b);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MutableRecord_ShouldProduceDiagnostic()
        {
            var test = @"
using System;
using System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record MutablePerson
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public void UpdatePerson(MutablePerson person)
    {
        person.Name = ""John""; // Should trigger diagnostic
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001")
                    .WithSpan(17, 17, 17, 29)
                    .WithArguments("UpdatePerson"));
        }

        [Test]
        public async Task RecordWithMixedProperties_ShouldProduceDiagnostic()
        {
            var test = @"
using System;
using System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public record Person
{
    public string Name { get; init; }
    private int age;
    public int Age 
    { 
        get => age;
        set => age = value; // Mutable property
    }
}

public class TestClass
{
    [EnforcePure]
    public void UpdateAge(Person person)
    {
        person.Age = 30; // Should trigger diagnostic
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("CS0518")
                    .WithSpan(10, 31, 10, 35)
                    .WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                DiagnosticResult.CompilerError("PMA0001")
                    .WithSpan(22, 17, 22, 26)
                    .WithArguments("UpdateAge"));
        }
    }
}