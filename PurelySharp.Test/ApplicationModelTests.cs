using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class ApplicationModelTests
    {
        // --- Blazor Component Methods (Usually Impure) ---
        // Blazor lifecycle methods (OnInitializedAsync, etc.) and event handlers (@onclick)
        // typically interact with state, UI, services, or JS interop, making them impure.
        // TODO: Add tests for Blazor component methods once analysis of UI frameworks is feasible.

        /*
        [Test]
        public async Task Blazor_EventHandler_Impure_Diagnostic() // Example placeholder
        {
            // Test code would involve a Blazor component (.razor) with:
            // @code {
            //   private int currentCount = 0;
            //   [EnforcePure] // This attribute wouldn't normally be here, just for testing
            //   private void IncrementCount() { currentCount++; } // Impure: Modifies component state
            // }
            // Expected Diagnostic: Impure state modification within IncrementCount
            Assert.Fail("Blazor test not implemented yet.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task Blazor_JsInterop_Impure_Diagnostic() // Example placeholder
        {
            // Test code would involve a Blazor component calling IJSRuntime:
            // @inject IJSRuntime JSRuntime
            // ...
            // [EnforcePure]
            // async Task CallJavaScript() { await JSRuntime.InvokeVoidAsync(""showAlert"", ""Hello""); } // Impure: JS Interop
            // Expected Diagnostic: Impure JS Interop call.
            Assert.Fail("Blazor JS Interop test not implemented yet.");
            await Task.CompletedTask;
        }
        */

        // --- Worker Service ExecuteAsync (Usually Impure) ---
        // The main loop of a Worker Service typically involves long-running tasks,
        // I/O, interaction with external services, etc., making it inherently impure.
        // TODO: Add tests for Worker Service ExecuteAsync once analysis of hosted services is feasible.

        /*
        [Test]
        public async Task WorkerService_ExecuteAsync_Impure_Diagnostic() // Example placeholder
        {
            // Test code would involve a class inheriting BackgroundService:
            // public class MyWorker : BackgroundService {
            //   [EnforcePure] // For testing only
            //   protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            //     while (!stoppingToken.IsCancellationRequested) {
            //       Console.WriteLine(""Worker running."'); // Impure: IO
            //       await Task.Delay(1000, stoppingToken); // Impure: Timing/Task
            //     }
            //   }
            // }
            // Expected Diagnostic: Impure Console.WriteLine or Task.Delay call.
            Assert.Fail("Worker Service test not implemented yet.");
            await Task.CompletedTask;
        }
        */
    }
} 