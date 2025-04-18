using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DiagnosticsTests
    {
        // --- Stopwatch Tests ---

        // TODO: Enable once analyzer recognizes Stopwatch methods as impure
        /*
        [Test]
        public async Task Stopwatch_StartNew_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



public class TestClass
{
    [EnforcePure]
    public Stopwatch TestMethod()
    {
        return Stopwatch.StartNew(); // Impure: Relies on system timer
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 37).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Stopwatch_InstanceMethods_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(Stopwatch sw)
    {
        sw.Start(); // Impure
        var e = sw.Elapsed; // Impure
        sw.Stop(); // Impure
    }
}";
            // Expect diagnostic on the first impure call (sw.Start())
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 18).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Process Tests ---
        // TODO: Enable once analyzer recognizes Process methods as impure
        /*
        [Test]
        public async Task Process_Start_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Process.Start(""notepad.exe""); // Impure: OS interaction
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 37).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Process_GetCurrentProcess_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



public class TestClass
{
    [EnforcePure]
    public Process TestMethod()
    {
        return Process.GetCurrentProcess(); // Impure: Reads external OS state
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 42).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        
        [Test]
        public async Task Process_Kill_Diagnostic()
        {
             var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(Process p)
    {
        p.Kill(); // Impure: OS interaction
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 16).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 