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
            var test = @"
using System;
using PurelySharp.Attributes;

public class PureDisposable : IDisposable
{
    public void Dispose() { } // Empty dispose method is pure
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        using (var disposable = new PureDisposable()) // Pure using statement
        {
            // No impure operations
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithUsing_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        using (var file = File.OpenRead(""test.txt""))
        {
            // Some operation
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithUsingAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;

public class PureDisposable : IDisposable
{
    public void Dispose() { } // Empty dispose method is pure
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        using (var disposable = new PureDisposable())
        {
            Console.WriteLine(""Inside using"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


