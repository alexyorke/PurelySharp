using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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
    public int TestMethod(int a, int b)
    {
        // Create a pure delegate that performs pure operations
        MathOperation add = (x, y) => x + y;
        
        // Invoke the pure delegate - should be considered pure
        return add(a, b);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(18, 16, 18, 19)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PassingDelegateAsArgument_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public int[] TestMethod(int[] numbers)
    {
        // Pure use of delegates as arguments to LINQ methods
        return numbers.Where(n => n > 0)
                     .Select(n => n * 2)
                     .ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int UsePureDelegate(int value)
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

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(24, 23, 24, 30)
                .WithArguments("UsePureDelegate");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
    public int TestMethod(int value)
    {
        // Use the higher-order function
        var multiplier = CreateMultiplier(10);
        return multiplier(value);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(21, 16, 21, 26)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
    public Func<int, int> CreateImpureDelegate()
    {
        // This delegate captures and modifies _counter
        return x => {
            _counter++; // Impure operation
            return x + _counter;
        };
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(16, 13)
                .WithArguments("CreateImpureDelegate");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}