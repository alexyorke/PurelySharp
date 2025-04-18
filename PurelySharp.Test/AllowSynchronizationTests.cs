using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AllowSynchronizationTests
    {
        [Test]
        public async Task PureMethodWithReadonlyLock_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private readonly int[] _array = new int[10];

    [EnforcePure]
    [AllowSynchronization]
    public int PureMethodWithLock()
    {
        lock (_lock)
        {
            return _array[0]; // Pure operation - just reading
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithImpureOperationInLock_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private int _counter = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void ImpureMethodWithLock()
    {
        lock (_lock)
        {
            _counter++; // This is impure because it modifies state
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(21, 13, 21, 23).WithArguments("ImpureMethodWithLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


