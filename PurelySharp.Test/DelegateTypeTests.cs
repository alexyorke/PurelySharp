using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DelegateTypeTests
    {
        [Test]
        public async Task DelegateTypeDefinition_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



// Delegate type definition
public delegate int MathOperation(int x, int y);

public class TestClass
{
    [EnforcePure]
    public MathOperation GetAddOperation()
    {
        // Return a pure delegate
        return (x, y) => x + y;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureDelegateWithPureOperation_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public delegate int MathOperation(int x, int y);

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(int a, int b)
    {
        // Create a pure delegate that performs pure operations
        MathOperation add = (x, y) => x + y;
        
        // Invoke the pure delegate - should be considered pure
        return add(a, b);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PassingDelegateAsArgument_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public delegate int MyDelegate(int x);

public class TestClass
{
    [EnforcePure]
    public int Process(MyDelegate func, int value)
    {
        return func(value);
    }

    [EnforcePure]
    public int TestMethod()
    {
        MyDelegate square = x => x * x; // Lambda definition is pure
        return Process(square, 5); // Process invocation might be complex
    }
}
";

            // TestMethod uses a lambda and invokes Process which takes a delegate.
            // This pattern might not be fully verified by current rules, resulting in PS0002 for both methods.
            var expectedProcess = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                        .WithSpan(10, 16, 10, 23) // Span covers the method name 'Process'
                                        .WithArguments("Process");
            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                     .WithSpan(16, 16, 16, 26) // Span covers the method name 'TestMethod'
                                     .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedProcess, expectedTestMethod);
        }

        [Test]
        public async Task FuncAndActionDelegates_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Func<int, int> TestMethod()
    {
        // Return a pure Func delegate
        return x => x * 2;
    }
    
    [EnforcePure]
    public int {|PS0002:UsePureDelegate|}(int value)
    {
        // Create standard delegate types
        Func<int, int> doubler = x => x * 2;
        Func<int, int, int> adder = (x, y) => x + y;
        
        // Use the delegates in a pure way
        int doubled = doubler(value);
        int sum = adder(doubled, 5);
        
        return sum;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HigherOrderFunctions_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Func<int, int> CreateMultiplier(int factor)
    {
        // Higher-order function returning a pure delegate
        return x => x * factor;
    }
    
    [EnforcePure]
    public int {|PS0002:TestMethod|}(int value)
    {
        // Use the higher-order function
        var multiplier = CreateMultiplier(10);
        return multiplier(value);
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DelegateWithImpureCapture_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _counter;
    
    [EnforcePure]
    public Func<int, int> {|PS0002:CreateImpureDelegate|}()
    {
        // This delegate captures and modifies _counter
        return x => {
            _counter++; // Impure operation
            return x + _counter;
        };
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CombiningDelegates_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public delegate void Logger(string message);

public class TestClass
{
    [EnforcePure]
    public Logger CombineDelegates()
    {
        // Create pure delegates that simply return values
        Logger logger1 = message => { /* Pure operation */ };
        Logger logger2 = message => { /* Pure operation */ };
        
        // Combining delegates is pure
        Logger combined = logger1 + logger2;
        
        return combined;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}