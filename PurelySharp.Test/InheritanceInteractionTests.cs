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
        public async Task DeepInheritanceAndAbstractState_PureMethods_NoDiagnostic()
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
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(23, 17, 23, 26).WithArguments("LogStatus"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(49, 25, 49, 32).WithArguments("Process"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(57, 28, 57, 34).WithArguments("Format"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(75, 18, 75, 38).WithArguments("UseProcessorImpurely"),
            };



            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 28, 7, 32).WithArguments("get_Name");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expected.Append(expectedGetName).ToArray());
        }
    }
}