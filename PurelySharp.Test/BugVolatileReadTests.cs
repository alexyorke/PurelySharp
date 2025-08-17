using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using Microsoft.CodeAnalysis.Testing;

namespace PurelySharp.Test
{

    [TestFixture]
    public class BugVolatileReadTests
    {
        [Test]
        public async Task VolatileRead_ShouldTriggerDiagnostic()
        {


            var test = @"
using System;
using System.Threading;
using PurelySharp.Attributes;

public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int {|PS0002:ReadCounter|}()
    {
        // Impure: volatile read
        return _counter;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}