using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RefReadonlyParameterTests
    {
        [Test]
        public async Task PureMethodWithRefReadonlyParameter_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Sum(ref readonly int x, ref readonly int y)
    {
        // Only reading values, no modifications - this is pure
        return x + y;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_AccessingFields_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Point
{
    public int X;
    public int Y;
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:CalculateDistance|}(ref readonly Point p1, ref readonly Point p2)
    {
        // Accessing struct fields through ref readonly parameter - this is pure
        int dx = p1.X - p2.X;
        int dy = p1.Y - p2.Y;
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_AssigningLocally_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct LargeStruct
{
    public int Value1;
    public int Value2;
    public int Value3;
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:GetMax|}(ref readonly LargeStruct data)
    {
        // Creating local variable from ref readonly - this is pure
        LargeStruct localCopy = data;
        
        // Modifying local copy, not the original - this is pure
        int max = localCopy.Value1;
        if (localCopy.Value2 > max) max = localCopy.Value2;
        if (localCopy.Value3 > max) max = localCopy.Value3;
        
        return max;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodAttemptingToModifyRefReadonlyParameter_CompilationError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:Increment|}(ref readonly int x)
    {
        {|CS8331:x|}++; // CS8331: Cannot assign to variable 'x' or use it as the right hand side of a ref assignment because it is a readonly variable
    }
}";

            // Expect PS0002 from markup and the compiler error CS8331
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyParameter_PassingToAnotherMethodWithRef_CompilationError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Process(ref readonly int value)
    {
        // Passing ref readonly as ref causes CS8329
        return NeedsRef(ref {|CS8329:value|}); 
    }
    
    // Method expecting ref, not ref readonly
    private int {|PS0004:NeedsRef|}(ref int val)
    {
        return val * 2;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithRefReadonlyStruct_AccessingMethodsOnStruct_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public readonly struct ReadOnlyValue
{
    private readonly int _value;
    
    public ReadOnlyValue(int value)
    {
        _value = value;
    }
    
    // Assuming these methods are pure or marked [EnforcePure] elsewhere
    [EnforcePure] public int {|PS0002:GetValue|}() => _value;
    [EnforcePure] public ReadOnlyValue {|PS0002:Add|}(int amount) => new ReadOnlyValue(_value + amount);
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Process|}(ref readonly ReadOnlyValue value)
    {
        // Calling pure methods on the readonly struct is pure
        return value.GetValue() + value.Add(5).GetValue();
    }
}";

            // Expect no diagnostic as the called methods are pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodPassingRefReadonlyToImpureMethod_DiagnosticAndCompilationError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _state;

    [EnforcePure]
    // Added PS0002 markup (calling impure ModifyGlobalState)
    public int {|PS0002:Process|}(ref readonly int value) 
    {
        // Passing ref readonly as ref causes CS8329
        // Also, calling an impure method from pure context causes PS0002
        return ModifyGlobalState(ref {|CS8329:value|}); // Impurity comes from ModifyGlobalState
    }
    
    // Impure method (modifies state)
    private int ModifyGlobalState(ref int val) 
    {
        _state++; 
        return val + _state;
    }
}";

            // Expect PS0002 from markup and the compiler error CS8329
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


