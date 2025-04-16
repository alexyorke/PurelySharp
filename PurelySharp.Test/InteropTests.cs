using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class InteropTests
    {
        // --- P/Invoke Tests (Impure) ---
        // P/Invoke calls are inherently impure as they interact with unmanaged code/OS.
        // TODO: Enable tests once analyzer can detect P/Invoke calls.

        /*
        [DllImport("user32.dll")]
        static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        [Test]
        public async Task PInvoke_MessageBox_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    [EnforcePure]
    public int TestMethod()
    {
        return MessageBox(IntPtr.Zero, ""Hello"", ""Purity Check"", 0); // Impure: P/Invoke call
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(17, 16, 17, 65).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- COM Interop Tests (Impure) ---
        // COM Interop involves interacting with external COM components, which is impure.
        // TODO: Add tests for COM Interop usage once analyzer support is considered.
        // (Requires setting up COM references/registration, potentially complex for unit tests).

        /*
        [Test]
        public async Task ComInterop_CreateObject_Diagnostic() // Example placeholder
        {
           // Test code would involve:
           // 1. Referencing a COM library (e.g., Microsoft Excel Object Library)
           // 2. Creating an instance: var excelApp = new Microsoft.Office.Interop.Excel.Application();
           // 3. Calling methods: excelApp.Visible = true;
           // Expected Diagnostic: Impure object creation/method call.
           Assert.Fail("COM Interop test not implemented yet.");
           await Task.CompletedTask;
        }
        */
    }
} 