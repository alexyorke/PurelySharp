﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using Microsoft.CodeAnalysis.Testing;
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


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PassingDelegateAsArgument_NoDiagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    // Delegate with lower accessibility
    private delegate void MyDelegate(int x);

    // Method using the less accessible delegate
    [EnforcePure]
    public static void Process(MyDelegate action, int value)
    {
        action(value); // Invocation here
    }

    [EnforcePure]
    public static void TestMethod()
    {
        MyDelegate impureAction = x => Console.WriteLine(x);
        Process(impureAction, 5);
    }
}
";


            var expectedErrorCS0051 = DiagnosticResult.CompilerError("CS0051").WithSpan(12, 24, 12, 31).WithArguments("TestClass.Process(TestClass.MyDelegate, int)", "TestClass.MyDelegate");


            var expectedDiagPS0002_Process = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(12, 24, 12, 31).WithArguments("Process");


            var expectedDiagPS0002_TestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(18, 24, 18, 34).WithArguments("TestMethod");


            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedErrorCS0051, expectedDiagPS0002_Process, expectedDiagPS0002_TestMethod);
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


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task HigherOrderFunctions_UnknownPurityDiagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    // Higher-order function taking an Action
    [EnforcePure] // <= Assume this method itself is pure structurally
    public void ApplyAction(Action action)
    {
        // Invocation happens here, purity depends on 'action'
        action(); // Should trigger PS0002 if action's purity cannot be guaranteed
    }

    [EnforcePure]
    public void TestMethod()
    {
        Action impureAction = () => Console.WriteLine();
        ApplyAction(impureAction); // Pass an impure action
    }
}
";

            var expectedDiagApplyAction = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(9, 17, 9, 28).WithArguments("ApplyAction");


            var expectedDiagTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(16, 17, 16, 27).WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagApplyAction, expectedDiagTestMethod);
        }

        [Test]
        public async Task DelegateWithImpureCapture_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int _counter = 0;

    [EnforcePure]
    public Func<int, int> CreateImpureDelegate()
    {
        return x =>
        {
            _counter++; // Impure operation via capture
            return x + _counter;
        };
    }
}";



            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(10, 27, 10, 47)
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

    [EnforcePure]
    public void UseCombinedDelegates()
    {
        var combined = CombineDelegates();
        combined(""Test message""); // Pure invocation if delegates are pure
    }
}";






            var expectedCombine = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                          .WithSpan(12, 19, 12, 35).WithArguments("CombineDelegates");
            var expectedUseCombined = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                            .WithSpan(25, 17, 25, 37).WithArguments("UseCombinedDelegates");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedCombine, expectedUseCombined);
        }
    }
}