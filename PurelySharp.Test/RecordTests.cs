using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace PurelySharp.Test
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
                // Handle compiler errors related to IsExternalInit
                DiagnosticResult.CompilerError("CS0518").WithSpan(8, 29, 8, 33).WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                DiagnosticResult.CompilerError("CS0518").WithSpan(8, 39, 8, 42).WithArguments("System.Runtime.CompilerServices.IsExternalInit"));
        }

        [Test]
        public async Task RecordWithPureMethod_Diagnostic()
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

            var expected = VerifyCS.Diagnostic().WithSpan(19, 16, 19, 30).WithArguments("UseCalculator");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
                VerifyCS.Diagnostic().WithSpan(19, 9, 19, 29).WithArguments("UpdatePerson"));
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
                // We need to handle both the compiler error and the analyzer diagnostic
                DiagnosticResult.CompilerError("CS0518").WithSpan(10, 31, 10, 35).WithArguments("System.Runtime.CompilerServices.IsExternalInit"),
                VerifyCS.Diagnostic().WithSpan(24, 9, 24, 24).WithArguments("UpdateAge"));
        }
    }
}


