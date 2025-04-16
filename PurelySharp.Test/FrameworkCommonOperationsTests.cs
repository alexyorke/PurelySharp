using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

namespace MockFramework
{
    public class Button { public string Content { get; set; } = """"; public event System.EventHandler? Click; }
    public class TextBox { public string Text { get; set; } = """"; } // Keep
    public class MessageBox { public static void Show(string text) {} } // Keep
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void UpdateUI(Button button)
    {
        button.Content = ""Clicked""; // Impure: UI Side Effect (Line 22 in this string)
    }
}";
            // Analyzer flags property assignment, adjust expected span again
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(22, 9, 22, 35) // Corrected end column to 35
                .WithArguments("UpdateUI");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        // ... rest of the file ...
    }
}