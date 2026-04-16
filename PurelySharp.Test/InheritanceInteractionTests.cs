using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Collections.Generic;
using System;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InheritanceInteractionTests
    {
        [Test]
        public async Task DeepInheritanceAndAbstractState_MissingAttributeDiagnostics()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public abstract class Device
{
    public Guid DeviceId { get; } // PS0004 get
    protected Device(Guid id) { DeviceId = id; } // PS0004 .ctor
    [EnforcePure] public abstract string GetStatus();
    // [EnforcePure] // Removed - Expect PS0004
    public Guid GetDeviceId() => DeviceId; // PS0004 Method
}

public abstract class NetworkedDevice : Device
{
    public string IPAddress { get; } // PS0004 get
    protected NetworkedDevice(Guid id, string ip) : base(id) { IPAddress = ip; } // PS0004 .ctor
    [EnforcePure] public override string GetStatus() => $""Device {base.DeviceId} online at {IPAddress}"";
    [EnforcePure] public abstract bool Ping();
    // [EnforcePure] // Removed - Expect PS0004
    public string GetIpAddress() => IPAddress; // PS0004 Method
}

public class SmartLight : NetworkedDevice
{
    public int Brightness { get; } // PS0004 get
    public SmartLight(Guid id, string ip, int brightness) : base(id, ip) { Brightness = brightness; } // PS0004 .ctor
    [EnforcePure] public override bool Ping() => IPAddress != null && IPAddress.Length > 0;
    // [EnforcePure] // Removed - Expect PS0004
    public int GetBrightness() => Brightness; // PS0004 Method
}

public class TestManager
{
    [EnforcePure]
    public string CheckLightStatus(SmartLight light) => light.GetStatus();

    [EnforcePure]
    public bool PingLight(SmartLight light) => light.Ping();

    [EnforcePure]
    public string GetFullLightDetails(SmartLight light)
    {
        Guid id = light.GetDeviceId();
        string ip = light.GetIpAddress();
        int brightness = light.GetBrightness();
        return $""ID: {id}, IP: {ip}, Brightness: {brightness}"";
    }
}
";

            var expectedGetDeviceIdProp = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 17, 7, 25).WithArguments("get_DeviceId");
            var expectedCtorDevice = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 15, 8, 21).WithArguments(".ctor");
            var expectedGetDeviceIdMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 17, 11, 28).WithArguments("GetDeviceId");
            var expectedGetIpAddressProp = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(16, 19, 16, 28).WithArguments("get_IPAddress");
            var expectedCtorNetworked = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(17, 15, 17, 30).WithArguments(".ctor");
            var expectedGetIpAddressMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(21, 19, 21, 31).WithArguments("GetIpAddress");
            var expectedGetBrightnessProp = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(26, 16, 26, 26).WithArguments("get_Brightness");
            var expectedCtorLight = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(27, 12, 27, 22).WithArguments(".ctor");
            var expectedGetBrightnessMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(30, 16, 30, 29).WithArguments("GetBrightness");

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedGetDeviceIdProp,
                                             expectedCtorDevice,
                                             expectedGetDeviceIdMethod,
                                             expectedGetIpAddressProp,
                                             expectedCtorNetworked,
                                             expectedGetIpAddressMethod,
                                             expectedGetBrightnessProp,
                                             expectedCtorLight,
                                             expectedGetBrightnessMethod);
        }

        [Test]
        public async Task AbstractClassWithMixedPurity_Diagnostics()
        {
            var testCode = @"
using PurelySharp.Attributes;
using System;

public abstract class DataProcessor
{
    public abstract string Name { get; }

    [EnforcePure] // Abstract method - assumed pure if not overridden impurely
    public abstract int Process(int data);

    [EnforcePure] // Virtual method with pure base implementation
    public virtual string Format(int data) => data.ToString();

    [EnforcePure] // Concrete method calling abstract Process
    public int ProcessAndDouble(int data)
    {
        return Process(data) * 2;
    }

    // Concrete impure method
    [EnforcePure]
    public void LogStatus(string status)
    {
        Console.WriteLine($""{Name}: {status}""); // Impure
    }
}

public class DoublingProcessor : DataProcessor
{
    public override string Name => ""Doubler"";

    // Pure implementation of abstract method
    [EnforcePure]
    public override int Process(int data) => data * 2;

    // Pure override of virtual method
    [EnforcePure]
    public override string Format(int data) => $""Data={data}"";
}

public class AddingProcessor : DataProcessor
{
    public override string Name => ""Adder"";
    private int _offset = 5; // Instance state

    // Impure implementation of abstract method
    [EnforcePure]
    public override int Process(int data)
    {
        _offset++; // Modifies state
        return data + _offset;
    }

    // Impure override of virtual method
    [EnforcePure]
    public override string Format(int data)
    {
        Console.WriteLine(""Formatting...""); // Impure call
        return $""Value: {data}"";
    }
}

public class TestUsage
{
     [EnforcePure]
     public int UseProcessorPurely(DataProcessor p, int value)
     {
         int processed = p.ProcessAndDouble(value);
         string formatted = p.Format(processed);
         return formatted.Length;
     }

     [EnforcePure]
     public void UseProcessorImpurely(DataProcessor p, string msg)
     {
         // Calls impure LogStatus
         p.LogStatus(msg);
     }
}
";

            var expected = new[]
            {
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(13, 27, 13, 33).WithArguments("Format"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(23, 17, 23, 26).WithArguments("LogStatus"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(49, 25, 49, 32).WithArguments("Process"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(57, 28, 57, 34).WithArguments("Format"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(67, 17, 67, 35).WithArguments("UseProcessorPurely"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(75, 18, 75, 38).WithArguments("UseProcessorImpurely"),
            };



            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 28, 7, 32).WithArguments("get_Name");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expected.Append(expectedGetName).ToArray());
        }

        [Test]
        public async Task PrivateProtectedVirtualDispatch_ResolvesWithinCompilationAndCanBePure()
        {
            var test = @"
using PurelySharp.Attributes;

public class BaseComponent
{
    [EnforcePure]
    private protected virtual int Compute(int value)
    {
        return value + 1;
    }

    [EnforcePure]
    public int ReadValue(int value)
    {
        return Compute(value) * 2;
    }
}

public class DerivedComponent : BaseComponent
{
    [EnforcePure]
    private protected override int Compute(int value)
    {
        return value * 3;
    }
}

public class Consumer
{
    [EnforcePure]
    public int Snapshot(int input)
    {
        var component = new DerivedComponent();
        return component.ReadValue(input);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ProtectedVirtualDispatch_DefaultConservativeImpure()
        {
            var test = @"
using PurelySharp.Attributes;

public class BaseWorker
{
    protected virtual int Compute(int value)
    {
        return value;
    }
}

public class WorkerHost : BaseWorker
{
    [EnforcePure]
    public int {|PS0002:ComputeWithProtectedVirtual|}(int value)
    {
        return Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PublicBasePrivateProtectedVirtualDispatch_ResolvesWithinCompilationAndCanBePure()
        {
            var test = @"
using PurelySharp.Attributes;

public class BaseComponent
{
    [EnforcePure]
    private protected virtual int Compute(int value)
    {
        return value + 1;
    }

    [EnforcePure]
    public int Snapshot(int value)
    {
        return Compute(value) * 2;
    }
}

public class DerivedComponent : BaseComponent
{
    [EnforcePure]
    private protected override int Compute(int value)
    {
        return value * 3;
    }
}

public class Consumer
{
    [EnforcePure]
    public int ReadValue(BaseComponent component, int value)
    {
        return component.Snapshot(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task GenericSealedTypeConstraint_PureOverrideCanBeConsideredPure()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class BaseCounter
{
    public virtual int Compute(int value)
    {
        Console.WriteLine(value);
        return value + 1;
    }
}

public sealed class PureCounter : BaseCounter
{
    public override int Compute(int value)
    {
        return value * 2;
    }
}

public class PureHost
{
    [EnforcePure]
    public int Process<T>(T counter, int value) where T : PureCounter
    {
        return counter.Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task GenericSealedTypeConstraint_TransitiveConstraint_PureOverrideCanBeConsideredPure()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class BaseCounter
{
    public virtual int Compute(int value)
    {
        Console.WriteLine(value);
        return value + 1;
    }
}

public sealed class PureCounter : BaseCounter
{
    public override int Compute(int value)
    {
        return value * 2;
    }
}

public class PureHost
{
    [EnforcePure]
    public int Process<T, U>(T counter, int value)
        where T : U
        where U : PureCounter
    {
        return counter.Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task GenericSealedTypeConstraint_TripleTransitiveConstraint_PureOverrideCanBeConsideredPure()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class BaseCounter
{
    public virtual int Compute(int value)
    {
        Console.WriteLine(value);
        return value + 1;
    }
}

public sealed class PureCounter : BaseCounter
{
    public override int Compute(int value)
    {
        return value * 2;
    }
}

public class PureHost
{
    [EnforcePure]
    public int Process<T, U, V>(T counter, int value)
        where T : U
        where U : V
        where V : PureCounter
    {
        return counter.Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task GenericSealedTypeConstraint_WithAsCastToInterface_NoConservativeDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public interface ICounter
{
    int Compute(int value);
}

public class BaseCounter
{
    public virtual int Compute(int value)
    {
        return value + 1;
    }
}

public sealed class PureCounter : BaseCounter, ICounter
{
    public override int Compute(int value)
    {
        return value * 2;
    }
}

public class PureHost
{
    [EnforcePure]
    public int Process<T>(T counter, int value) where T : PureCounter
    {
        return (counter as ICounter)!.Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SealedOverride_ConcreteReceiverCanBePure()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class BaseWorker
{
    public virtual int Compute(int value)
    {
        Console.WriteLine(value);
        return value;
    }
}

public class PureWorker : BaseWorker
{
    public sealed override int Compute(int value)
    {
        return value + 1;
    }
}

public class BadWorker : BaseWorker
{
    public override int Compute(int value)
    {
        Console.WriteLine(value);
        return value + 2;
    }
}

public class WorkerHost
{
    [EnforcePure]
    public int Process(PureWorker worker, int value)
    {
        return worker.Compute(value);
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
