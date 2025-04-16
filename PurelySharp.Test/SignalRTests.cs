using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
// using Microsoft.AspNetCore.SignalR.Client; // Requires SignalR packages
// using Microsoft.AspNetCore.SignalR;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class SignalRTests
    {
        // Note: These tests require adding SignalR packages (Client and/or Core).
        // They serve as placeholders for future analysis if SignalR support is added.
        // Typical SignalR hub methods, client invocations, and message handlers are impure
        // due to network I/O and potential state manipulation.

        // TODO: Add real tests if SignalR analysis becomes a priority.

        /*
        // Example Hub (for context)
        public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
        {
            // Usually impure: Sends message over network, might access state
            public async Task SendMessage(string user, string message)
            {
                await Clients.All.SendAsync("ReceiveMessage", user, message);
            }
        }

        [Test]
        public async Task SignalR_ServerHubMethod_Diagnostic() // Example placeholder
        {
            // Test would analyze the SendMessage method above, potentially applying [EnforcePure] to it.
            // Expected Diagnostic: Impurity due to Clients.All.SendAsync (Network I/O).
            var test = @"
#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class ChatHub : Hub
{
    [EnforcePure] // Applied for testing
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync(""ReceiveMessage"", user, message); // Impure: Network I/O
    }
}";
            // Span would target the SendAsync call
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 15, 15, 69).WithArguments("SendMessage");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires SignalR references
            Assert.Inconclusive("SignalR Hub method test needs SignalR references.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task SignalR_ClientInvokeAsync_Diagnostic() // Example placeholder
        {
            // Test setup would involve HubConnection
            // var connection = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()...Build();
            // [EnforcePure]
            // async Task CallHub() { await connection.InvokeAsync(""SendMessage"", ""user"", ""message""); } // Impure: Network I/O
            
            Assert.Fail("SignalR Client test not implemented yet.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task SignalR_ClientOnHandler_Diagnostic() // Example placeholder
        {
            // Test setup involves HubConnection.On()
            // var connection = ...;
            // [EnforcePure] // Applied to the handler for testing
            // Action<string, string> handler = (user, message) => { 
            //    Console.WriteLine($""{user}: {message}""); // Impure action within handler
            // };
            // connection.On(""ReceiveMessage"", handler);
            // The impurity lies within the handler's execution, triggered by external network events.
            
            Assert.Fail("SignalR Client handler test not implemented yet.");
            await Task.CompletedTask;
        }
        */
    }
} 