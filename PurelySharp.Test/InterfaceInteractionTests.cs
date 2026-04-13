using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InterfaceInteractionTests
    {
        [Test]
        public async Task PureInterfaceImplementation_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public interface ICalculator
{
    [EnforcePure] int Add(int a, int b);
    [EnforcePure] int Multiply(int a, int b);
}

public class SimpleCalculator : ICalculator
{
    public int Add(int a, int b) => a + b;

    public int Multiply(int a, int b) => a * b;
}

public class Usage
{
    [EnforcePure]
    public int UseCalculator(ICalculator calc, int x, int y)
    {
        return calc.Multiply(calc.Add(x, y), 2);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureInterfaceImplementation_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public interface ILogger
{
    [EnforcePure] void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void {|PS0002:Log|}(string message)
    {
        Console.WriteLine(message); // Impure call
    }
}

public class Service
{
    private ILogger _logger;
    public {|PS0004:Service|}(ILogger logger) { _logger = logger; }

    [EnforcePure]
    public void DoWork(string data)
    {
        // This call becomes impure because the underlying Log is impure
        _logger.Log($""Processing: {data}"");
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
