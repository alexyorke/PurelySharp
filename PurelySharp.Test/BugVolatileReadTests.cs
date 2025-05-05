using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using Microsoft.CodeAnalysis.Testing;

namespace PurelySharp.Test
{
    /// <summary>
    /// Regression tests that currently fail, demonstrating analyzer gaps.
    /// </summary>
    [TestFixture]
    public class BugVolatileReadTests
    {
        [Test]
        [Ignore("Test is currently failing.")]
        public async Task VolatileRead_ShouldTriggerDiagnostic()
        {
            // Reading a volatile field is impure because it performs a memory barrier.
            // The analyzer currently misses this, so this test should fail until the bug is fixed.
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