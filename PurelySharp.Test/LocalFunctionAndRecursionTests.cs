using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LocalFunctionAndRecursionTests
    {
        [Test]
        public async Task ImpureLocalFunction_FieldModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _field;

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        int LocalFunction()
        {
            _field++; // Local function modifies field
            return _field;
        }

        return LocalFunction();
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MethodWithRecursiveImpureCall_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(int n)
    {
        if (n <= 0) return;
        Console.WriteLine(n); // Impure operation
        TestMethod(n - 1); // Recursive call
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private void ImpureMethod()
    {
        Console.WriteLine(""Impure"");
    }

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        ImpureMethod(); // Calling impure method
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


