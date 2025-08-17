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

        [Test]
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
    public void UpdateUI(Button button)
    {
        button.Content = ""Clicked""; // Impure: UI Side Effect (Line 22 in this string)
    }
}";

            var expectedGetContent = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(10, 41, 10, 48).WithArguments("get_Content");
            var expectedGetText = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 42, 11, 46).WithArguments("get_Text");
            var expectedShow = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(12, 50, 12, 54).WithArguments("Show");
            var expectedUpdateUI = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(20, 17, 20, 25).WithArguments("UpdateUI");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetContent, expectedGetText, expectedShow, expectedUpdateUI });
        }

        [Test]
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

            var expectedGetContent = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(10, 41, 10, 48).WithArguments("get_Content");
            var expectedGetText = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 42, 11, 46).WithArguments("get_Text");
            var expectedShow = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(12, 50, 12, 54).WithArguments("Show");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetContent, expectedGetText, expectedShow);
        }




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

            var expectedGetItem = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(10, 47, 10, 51).WithArguments("get_Item");
            var expectedGetSection1 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(10, 95, 10, 105).WithArguments("GetSection");
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 54, 11, 59).WithArguments("get_Value");
            var expectedGetSection2 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(16, 38, 16, 48).WithArguments("GetSection");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetItem, expectedGetSection1, expectedGetValue, expectedGetSection2);
        }














































    }
}