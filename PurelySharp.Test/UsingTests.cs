using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UsingTests
    {
        [Test]
        public async Task PureMethodWithUsing_NoDiagnostic()
        {
            var code = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        using (var disposable = new PureDisposable()) // Pure disposable, Dispose is pure
        {
            return 1; // Body is pure
        }
    }
}

public class PureDisposable : IDisposable
{
    // Dispose is implicitly pure (empty body)
    public void Dispose() { }
}";

            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                    .WithSpan(20, 17, 20, 24)
                                    .WithArguments("Dispose");
            await VerifyCS.VerifyAnalyzerAsync(code, expectedPS0004);
        }

        [Test]
        public async Task ImpureMethodWithUsing_Diagnostic()
        {
            var test = @$"
using System;
using PurelySharp.Attributes;
using System.IO;

public class TestClass
{{
    [EnforcePure]
    public void TestMethod()
    {{
        using (var file = File.OpenRead(""test.txt"")) // Impure resource acquisition
        {{
            // Some operation
        }}
    }}
}}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                  .WithSpan(9, 17, 9, 27)
                                  .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithUsingAndImpureOperation_Diagnostic()
        {
            var test = @$"
using System;
using PurelySharp.Attributes;
using System.IO;

public class PureDisposable : IDisposable
{{
    public void Dispose() {{ }} // Empty dispose method is pure
}}

public class TestClass
{{
    [EnforcePure]
    public void TestMethod()
    {{
        using (var disposable = new PureDisposable())
        {{
            Console.WriteLine(""Inside using""); // Impure operation inside body
        }}
    }}
}}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(14, 17, 14, 27)
                                   .WithArguments("TestMethod");


            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                    .WithSpan(8, 17, 8, 24)
                                    .WithArguments("Dispose");

            await VerifyCS.VerifyAnalyzerAsync(test, expected, expectedPS0004);
        }
    }
}


