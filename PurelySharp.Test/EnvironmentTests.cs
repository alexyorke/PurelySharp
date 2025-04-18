using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EnvironmentTests
    {
        // TODO: Enable tests below once analyzer recognizes Environment methods as impure/pure

        /*
        [Test]
        public async Task Environment_GetFolderPath_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        // Impure: Reads OS state
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 70).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Environment_GetEnvironmentVariable_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string? TestMethod()
    {
        // Impure: Reads OS state
        return Environment.GetEnvironmentVariable(""PATH"");
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 55).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        
        [Test]
        public async Task Environment_Exit_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // Impure: Major side effect
        Environment.Exit(0);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 27).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        
        [Test]
        public async Task Environment_CurrentManagedThreadId_Diagnostic()
        {
             var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        // Impure: Depends on runtime scheduler state
        return Environment.CurrentManagedThreadId;
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 16, 13, 48).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        [Test]
        public async Task Environment_ProcessorCount_NoDiagnostic()
        {
            // Treated as pure as it usually returns a stable value read at startup
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Environment.ProcessorCount;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
} 