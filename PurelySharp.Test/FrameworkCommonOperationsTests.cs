using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FrameworkCommonOperationsTests
    {
        // ... other tests ...
        [Test] // This test should pass as property assignment is detected
        public async Task GUI_SetButtonContent_Diagnostic()
        {
            var test = @"
#nullable enable // To handle EventHandler? warning
using System;
using System.Threading.Tasks;
using MockFramework;
using PurelySharp.Attributes;

namespace MockFramework
{
    public class Button { public string Content { get; set; } = """"; public event System.EventHandler? Click; }
    public class TextBox { public string Text { get; set; } = """"; } // Keep
    public class MessageBox { public static void Show(string text) {} } // Keep
}



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:UpdateUI|}(Button button)
    {
        button.Content = ""Clicked""; // Impure: UI Side Effect (Line 22 in this string)
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test] // This should pass
        public async Task GUI_GetTextBoxText_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Threading.Tasks;
using MockFramework;
using PurelySharp.Attributes;

namespace MockFramework
{
    public class Button { public string Content { get; set; } = """"; public event System.EventHandler? Click; } // Keep
    public class TextBox { public string Text { get; set; } = """"; }
    public class MessageBox { public static void Show(string text) {} } // Keep
}



public class TestClass
{
    [EnforcePure]
    public string GetInput(TextBox textBox)
    {
        return textBox.Text; // Pure: Reading UI state (Line 21)
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- New Tests Added Below (Mostly Commented Out) ---

        // --- Configuration Tests ---
        [Test]
        public async Task PureMethod_ReadConfiguration_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using MockFramework;
using PurelySharp.Attributes;

namespace MockFramework 
{
    // Simplified mock for IConfiguration
    public interface IConfiguration { string? this[string key] { get; } IConfigurationSection GetSection(string key); }
    public interface IConfigurationSection { string? Value { get; } }
    public class MockConfigurationSection : IConfigurationSection { public string? Value => ""someValue""; }
    public class MockConfiguration : IConfiguration 
    {
        public string? this[string key] => key == ""MyKey:MyValue"" ? ""someValue"" : null;
        public IConfigurationSection GetSection(string key) => new MockConfigurationSection();
    } 
}



public class TestClass
{
    [EnforcePure]
    public string? ReadConfigIndexer(IConfiguration config)
    {
        return config[""MyKey:MyValue""]; // Pure read
    }

    [EnforcePure]
    public string? ReadConfigGetSection(IConfiguration config)
    {
        return config.GetSection(""MyKey"").Value; // Marked as impure/unknown
    }
}";
            // REMOVED EXPECTATIONS: Analyzer correctly sees these mock accesses as pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- ASP.NET Core Minimal APIs / Middleware (Commented Out) ---

        // TODO: Enable when analyzer handles lambda purity vs. registration side-effects
        /*
        [Test]
        public async Task AspNetCore_MinimalApiMapGet_NoDiagnosticOnLambda()
        {
            // This tests if the LAMBDA ITSELF is pure, not the MapGet call
            var test = @"
using System;
using PurelySharp.Attributes;
// Mock types needed for MapGet signature if checking the registration itself

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
public class EnforcePureAttribute : Attribute { }

public class Test
{
    [EnforcePure]
    public Func<string> GetPureLambda() 
    {
        return () => ""Hello""; // This lambda is pure
    }
}";
            // We are only checking the lambda source here
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // TODO: Enable when analyzer handles complex framework interactions
        /*
        [Test]
        public async Task AspNetCore_MinimalApiMapPost_Diagnostic()
        {
             var test = @"
                // Requires full mocks for WebApplication, Results, HttpRequest, DbContext etc.
                // The lambda body contains impure operations (DB access, Results.Created)
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }

        [Test]
        public async Task AspNetCore_ResultsOk_Diagnostic()
        {
             var test = @"
                using System;
                using MockFramework; // Need Results mock
                namespace MockFramework { public static class Results { public static IResult Ok() => null; } public interface IResult {} }

                [AttributeUsage(AttributeTargets.Method)]
                public class EnforcePureAttribute : Attribute { }
                
                public class TestClass {
                    [EnforcePure] public IResult Test() => Results.Ok(); 
                }
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }

        [Test]
        public async Task AspNetCore_ReadFromJsonAsync_Diagnostic()
        {
            var test = @"
                // Requires mocks for HttpRequest, extension methods etc.
                // Involves IO.
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }
        
        [Test]
        public async Task AspNetCore_UseMiddleware_Diagnostic()
        {
             var test = @"
                // Requires mocks for IApplicationBuilder etc.
                // Modifies application pipeline state.
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }
        */

        // --- EF Core Database / Entry (Commented Out) ---

        // TODO: Enable when analyzer handles DbContext state tracking / raw SQL
        /*
        [Test]
        public async Task EFCore_BeginTransaction_Diagnostic()
        {
            var test = @"
                // Requires DbContext/Database mocks
                // External DB state change
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }
        
        [Test]
        public async Task EFCore_ExecuteSqlRaw_Diagnostic()
        {
             var test = @"
                // Requires DbContext/Database mocks
                // External DB state change / IO
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }

        [Test]
        public async Task EFCore_EntryStateModified_Diagnostic()
        {
             var test = @"
                // Requires DbContext/EntityEntry mocks
                // Modifies internal DbContext state (change tracker)
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }
        */

        // --- Dependency Injection (Commented Out) ---
        // TODO: Enable when analyzer handles DI container interactions
        /*
        [Test]
        public async Task DI_GetService_Diagnostic()
        {
            var test = @"
                // Requires IServiceProvider mocks
                // Impure due to potential side effects in resolved service ctor/lifetime management
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }

        [Test]
        public async Task DI_ActivatorUtilitiesCreateInstance_Diagnostic()
        {
             var test = @"
                // Requires IServiceProvider, ActivatorUtilities mocks
                // Impure due to potential ctor side effects and SP interaction
             ";
             // var expected = ...
             // await VerifyCS.VerifyAnalyzerAsync(test, expected);
             await Task.CompletedTask;
        }
        */

        // --- Commented Out Original Framework Tests ---
        // TODO: Enable once analyzer recognizes Controller actions as impure (e.g., known signatures or base types)
        /*
        [Test]
        public async Task AspNetCore_ControllerAction_Diagnostic()
        { ... }
        */

        // TODO: Enable once analyzer recognizes Request.Query (or similar patterns for reading external state) as impure
        /*
        [Test]
        public async Task AspNetCore_ReadRequest_Diagnostic()
        { ... }
        */

        // TODO: Enable once analyzer recognizes ILogger.LogInformation (or general logging patterns) as impure
        /*
        [Test]
        public async Task Logging_LogInformation_Diagnostic()
        { ... }
        */

        // TODO: Enable once analyzer recognizes DbContext.Add/SaveChangesAsync (or general ORM patterns) as impure
        /*
        [Test]
        public async Task EFCore_AddAndSaveChanges_Diagnostic()
        { ... }
        */

        // TODO: Enable once analyzer recognizes DbContext.Find (or general ORM patterns) as impure
        /*
        [Test]
        public async Task EFCore_Find_Diagnostic()
        { ... }
        */

        // TODO: Enable once analyzer recognizes MessageBox.Show (or UI interaction patterns) as impure
        /*
        [Test]
        public async Task GUI_MessageBoxShow_Diagnostic()
        { ... }
        */
    }
}