using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FalseNegativeTests
    {
        [Test]
        public async Task ThrowOnlyMethod_NoDiagnostic_Bug()
        {
            // This test highlights that the analyzer currently treats 'throw' statements
            // as pure even though they change control-flow and should be considered impure.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:ThrowingMethod|}()
    {
        // Throwing an exception is impure (alters control-flow, allocates),
        // but the analyzer currently fails to report a diagnostic.
        throw new InvalidOperationException();
    }
}";
            // No diagnostic expected – the analyzer incorrectly considers the method pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DelegateInvocationOfImpureAction_NoDiagnostic_Bug()
        {
            // The analyzer does not follow delegate targets when the delegate is stored in a field.
            // Invoking such a delegate should therefore be reported as impure but currently is not.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    // Static readonly field hiding an impure Console.WriteLine call inside the delegate.
    private static readonly Action ImpureAction = () => Console.WriteLine(""Side-effect"");

    [EnforcePure]
    public void CallImpureDelegate()
    {
        // Invoking the delegate causes side-effects, but the analyzer misses it.
        ImpureAction();
    }
}";
            // No diagnostic expected – the analyzer incorrectly considers the method pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}