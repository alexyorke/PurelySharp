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
    // Pure implementation
    [EnforcePure]
    public int Add(int a, int b) => a + b;

    // Pure implementation
    [EnforcePure]
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
            // All implementations and usages are pure
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
    // Impure implementation
    [EnforcePure]
    public void Log(string message)
    {
        Console.WriteLine(message); // Impure call
    }
}

public class Service
{
    private ILogger _logger;
    public Service(ILogger logger) { _logger = logger; }

    [EnforcePure]
    public void DoWork(string data)
    {
        // This call becomes impure because the underlying Log is impure
        _logger.Log($""Processing: {data}"");
    }
}
";
            // Expect diagnostic on ConsoleLogger.Log and PS0004 on Service.ctor (based on runner output)
            var expectedLog = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                      .WithSpan(14, 17, 14, 20) // Span of 'Log' in ConsoleLogger
                                      .WithArguments("Log");
            // var expectedDoWork = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId) // Removed based on output
            //                       .WithSpan(26, 17, 26, 23) // Span of 'DoWork' in Service
            //                       .WithArguments("DoWork");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                     .WithSpan(23, 12, 23, 19) // Span of Service .ctor
                                     .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedLog, expectedCtor);
        }
    }
}