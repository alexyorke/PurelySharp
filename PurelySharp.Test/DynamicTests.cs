using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class DynamicTests
    {
        // --- Dynamic Operations (Impure) ---
        // Operations on `dynamic` objects involve runtime binding (DLR) which interacts
        // with runtime state and hides the exact operation, making static purity analysis difficult.
        // Therefore, dynamic operations are conservatively treated as impure.

        // TODO: Enable tests once analyzer flags dynamic operations as impure.
        /*
        [Test]
        public async Task Dynamic_MethodInvocation_Diagnostic()
        {
            var test = @"
#nullable enable
using System;



public class TestClass
{
    public void ImpureMethod() { Console.WriteLine(""Side effect!""); }

    [EnforcePure]
    public void TestMethod(dynamic d)
    {
        d.ImpureMethod(); // Impure: Dynamic dispatch
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 9, 15, 26).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Dynamic_PropertyAccess_Diagnostic()
        {
             var test = @"
#nullable enable
using System;



public class TestClass
{
     public int Value { get; set; } // Assume setter could be impure

    [EnforcePure]
    public void TestMethod(dynamic d, int val)
    {
        d.Value = val; // Impure: Dynamic property set
        var x = d.Value; // Impure: Dynamic property get (could invoke impure getter)
    }
}";
            // Expect diagnostic on the first dynamic operation (d.Value = val)
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 9, 15, 22).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Dynamic_Addition_Diagnostic() // Even simple ops are impure via DLR
        {
             var test = @"
#nullable enable
using System;



public class TestClass
{
    [EnforcePure]
    public object TestMethod(dynamic a, dynamic b)
    {
        return a + b; // Impure: Dynamic operator invocation
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 23).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 