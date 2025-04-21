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
        public async Task PureMethodWithNullPropagation_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}(Person? person)
    {
        // Null propagation itself is pure
        return person?.Name ?? ""Unknown"";
    }
    }
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullPropagation_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
    
    public void LogToConsole()
    {
        Console.WriteLine(Name);
    }
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Person? person)
    {
        // Null propagation followed by impure call
        person?.LogToConsole();
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithNullPropagationAndImpureOperation_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class Person
{
    public string Name { get; set; } = """";
    public int Age { get; set; }
}

public class TestClass
{
    private int _counter;

    [EnforcePure]
    public string {|PS0002:TestMethod|}(Person? person)
    {
        // Null propagation is pure
        var name = person?.Name ?? ""Unknown"";
        _counter++; // Impure state modification
        return name;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


