using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LockStatementTests
    {

        [Test]
        public async Task LockStatement_ImpureByDefault()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public void ImpureMethod()
    {
        lock (_lock)
        {
            Console.WriteLine(""Inside lock"");
        }
    }
}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(14, 17, 14, 29)
                                   .WithArguments("ImpureMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }



        [Test]
        public async Task LockStatement_WithPureOperations_ShouldBePure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics;



[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();
    private readonly int _value = 42;

    [EnforcePure]
    [AllowSynchronization]
    public int PureMethodWithLock()
    {
        int result;
        lock (_lock)
        {
            result = _value;
        }
        return result;
    }
}";





            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(18, 16, 18, 34)
                                   .WithArguments("PureMethodWithLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        [Test]
        public async Task LockStatement_WithPureOperations_CurrentBehavior()
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



            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(16, 16, 16, 34)
                                   .WithArguments("PureMethodWithLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        [Test]
        public async Task LockStatement_WithImpureOperations_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private readonly object _lock = new object();
    private int _value = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void ImpureMethodWithLock()
    {
        lock (_lock)
        {
            _value++; // This is impure because it modifies state
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(16, 17, 16, 37)
                                   .WithArguments("ImpureMethodWithLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        [Test]
        public async Task LockStatement_NonReadonlyObject_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

class Program
{
    private object _nonReadonlyLock = new object(); // Non-readonly lock object
    private int _counter = 0;

    [EnforcePure]
    [AllowSynchronization]
    public void ImpureMethodWithNonReadonlyLock()
    {
        lock (_nonReadonlyLock)
        {
            _counter++; // This is the impure operation
        }
    }
}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(16, 17, 16, 48)
                                   .WithArguments("ImpureMethodWithNonReadonlyLock");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


