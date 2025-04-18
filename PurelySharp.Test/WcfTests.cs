using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
// using System.ServiceModel; // Requires adding WCF packages
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class WcfTests
    {
        // Note: These tests require adding WCF packages (System.ServiceModel.Primitives, etc.)
        // and potentially more setup (defining service/data contracts).
        // They serve as placeholders for future analysis if WCF support is added.
        // All typical WCF operations (client calls, service implementations, hosting) are impure.

        // TODO: Add real tests if WCF analysis becomes a priority.

        /*
        // Example Service Contract Interface (for context)
        [System.ServiceModel.ServiceContract]
        public interface IMyService
        {
            [System.ServiceModel.OperationContract]
            string GetData(int value);
        }

        // Example Service Implementation (for context)
        public class MyService : IMyService
        {
            public string GetData(int value) 
            {
                Console.WriteLine($"GetData called with {value}"); // Impure: IO
                return $"You entered: {value}"; 
            }
        }

        [Test]
        public async Task Wcf_ClientProxyCall_Diagnostic() // Example placeholder
        {
            // Test setup would involve creating a client proxy (e.g., via ChannelFactory or generated client)
            // var factory = new System.ServiceModel.ChannelFactory<IMyService>(...);
            // IMyService client = factory.CreateChannel();
            // [EnforcePure]
            // void CallService() { string result = client.GetData(123); } // Impure: Network I/O
            
            Assert.Fail("WCF Client test not implemented yet.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task Wcf_ServiceHost_Diagnostic() // Example placeholder
        {
            // Test setup would involve ServiceHost
            // [EnforcePure]
            // void HostService() { 
            //    var host = new System.ServiceModel.ServiceHost(typeof(MyService)); // Impure: Network setup
            //    host.Open(); // Impure: Starts listening
            // }

            Assert.Fail("WCF Host test not implemented yet.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task Wcf_ServiceOperationImplementation_Diagnostic() // Example placeholder
        {
           var test = @"
#nullable enable
using System;
using System.ServiceModel;



[ServiceContract]
public interface IMyService { [OperationContract] string GetData(int value); }

public class MyService : IMyService 
{
    [EnforcePure] // Attribute applied to the implementation for testing
    public string GetData(int value)
    {
        Console.WriteLine(""Impure op""); // Impure: IO
        return value.ToString();
    }
}";
            // Expect diagnostic on the Console.WriteLine call
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(18, 9, 18, 37).WithArguments("GetData");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires WCF references
            Assert.Inconclusive("WCF Service implementation test needs WCF references.");
            await Task.CompletedTask;
        }
        */
    }
} 