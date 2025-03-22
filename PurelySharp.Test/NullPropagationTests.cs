using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullPropagationTests
    {
        [Test]
        public async Task PureMethodWithNullPropagation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public string TestMethod(Person person)
    {
        // Null propagation is considered pure
        return person?.Name ?? ""Unknown"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullPropagation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    
    public void LogToConsole()
    {
        Console.WriteLine(Name);
    }
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Person person)
    {
        // Null propagation with console write is impure
        person?.LogToConsole();
    }
}";

            // The analyzer doesn't detect the impurity with null propagation
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithNullPropagationAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class TestClass
{
    private int _counter;

    [EnforcePure]
    public string TestMethod(Person person)
    {
        // Null propagation is pure, but incrementing field is impure
        var name = person?.Name ?? ""Unknown"";
        _counter++; // This line is detected as impure
        return name;
    }
}";

            // Specify a diagnostic for the impure counter increment
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(22, 9, 22, 19)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


