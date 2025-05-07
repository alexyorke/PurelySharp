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
    public class CompositionInteractionTests
    {
        [Test]
        public async Task CompositionWithPureAndImpureCalls_Diagnostics()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class Engine
{
    [EnforcePure]
    public int GetHorsepower() => 150; // Pure

    [EnforcePure]
    public void Start() // Impure
    {
        Console.WriteLine(""Engine started"");
    }
}

public class Wheels
{
    [EnforcePure]
    public int GetDiameter() => 20; // Pure
}

public class Car
{
    private readonly Engine _engine = new Engine();
    private readonly Wheels _wheels = new Wheels();

    [EnforcePure]
    public int GetCarInfoPure() // Pure
    {
        return _engine.GetHorsepower() + _wheels.GetDiameter();
    }

    [EnforcePure]
    public void StartCar() // Impure (calls Engine.Start)
    {
        _engine.Start();
    }

    [EnforcePure]
    public int GetPowerToWheelRatio() // Impure (calls Engine.Start indirectly via StartCar) -> This is debatable, depends on analysis depth. Assume direct call impurity.
    {
        // Let's assume StartCar is impure, making this impure too if called.
        // For simplicity, let's test direct impure call:
        _engine.Start();
        return _engine.GetHorsepower() / _wheels.GetDiameter();
    }
}
";
            // Expect diagnostics on:
            // 1. Engine.Start (due to Console.WriteLine)
            // 2. Car.StartCar (calls impure Engine.Start)
            // 3. Car.GetPowerToWheelRatio (calls impure Engine.Start)
            var expectedEngineStart = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                              .WithSpan(11, 17, 11, 22) // Span of 'Start' in Engine
                                              .WithArguments("Start");
            var expectedCarStartCar = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                              .WithSpan(35, 17, 35, 25) // Span of 'StartCar' in Car
                                              .WithArguments("StartCar");
            var expectedGetPowerToWheelRatio = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                                       .WithSpan(41, 16, 41, 36) // CORRECTED End column 36
                                                       .WithArguments("GetPowerToWheelRatio");

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedEngineStart,
                                             expectedCarStartCar,
                                             expectedGetPowerToWheelRatio);
        }
    }
}