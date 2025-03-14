using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class PureMethodAnalyzerUnitTests
    {
        [TestMethod]
        public async Task EmptyMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task PureMethodWithReturn_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x, int y)
    {
        return x + y;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task PureMethodWithLocalVar_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x, int y)
    {
        var result = x * y;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ImpureMethodWithFieldAssignment_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public void TestMethod()
    {
        _field = 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task ImpureMethodWithConsoleWrite_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine(""Hello"");
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task PureMethodCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int Add(int x, int y)
    {
        return x + y;
    }

    [EnforcePure]
    public int AddAndMultiply(int x, int y, int z)
    {
        return Add(x, y) * z;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task PureMethodCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public void ImpureMethod()
    {
        Console.WriteLine(""Hello"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        ImpureMethod();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task ComplexPureLinqOperations_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(IEnumerable<int> numbers)
    {
        return numbers
            .Where(x => x > 0)
            .Select(x => x * x)
            .OrderBy(x => x)
            .Take(5)
            .Sum();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ComplexNestedExpressions_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x, double y, double z)
    {
        var a = Math.Sin(x) * Math.Cos(y);
        var b = Math.Pow(Math.E, z) / Math.PI;
        var c = Math.Sqrt(Math.Abs(a * b));
        return Math.Max(a, Math.Min(b, c));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task PurePropertyAccess_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Point
{
    public int X { get; }
    public int Y { get; }
    public double Length => Math.Sqrt(X * X + Y * Y);
}

public class TestClass
{
    [EnforcePure]
    public double TestMethod(Point p1, Point p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy) / (p1.Length + p2.Length);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task SimpleMathMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(x);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MathConstant_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod()
    {
        return Math.PI;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MathMethodChain_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        return Math.Sin(Math.Cos(x));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ComplexLinqWithMath_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(IEnumerable<double> numbers)
    {
        return numbers
            .Where(x => x > Math.PI)
            .Select(x => Math.Pow(Math.Sin(x), 2) + Math.Pow(Math.Cos(x), 2))
            .OrderBy(x => Math.Abs(x - 1))
            .Take(5)
            .Average();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RecursivePureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int Fibonacci(int n)
    {
        if (n <= 1) return n;
        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ComplexStringOperations_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        var words = input.Split(' ')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w.Trim().ToLower())
            .OrderBy(w => w.Length)
            .ThenBy(w => w);

        return string.Join("" "", words);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task ImpureMethodWithFileOperation_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(string path)
    {
        File.WriteAllText(path, ""test"");
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(11, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task ImpureMethodWithRandomOperation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return new Random().Next();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithMutableParameter_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Add(42); // Modifying input parameter is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(11, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithMutableStructParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public struct MutableStruct
{
    public int Value;
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(MutableStruct str)
    {
        str.Value = 42; // Modifying struct field is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithAsyncOperation_Diagnostic()
        {
            var test = @"
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public async Task<int> TestMethod()
    {
        await Task.Delay(100); // Async operation is impure
        return 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(11, 28)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithLazyEvaluation_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> numbers)
    {
        return numbers.Where(x => x > 0)
                     .Select(x => x * x)
                     .OrderBy(x => x);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithClosureCapture_Diagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> numbers)
    {
        return numbers.Select(x => x + _field); // Capturing mutable field
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 29)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithRecursiveImpureCall_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int n)
    {
        if (n <= 0) return 0;
        Console.WriteLine(n); // Impure operation
        return TestMethod(n - 1);
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithDelegateInvocation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private Action _action = () => Console.WriteLine(""Hello"");

    [EnforcePure]
    public void TestMethod()
    {
        _action(); // Invoking delegate that could be impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithEventSubscription_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    public event EventHandler MyEvent;

    [EnforcePure]
    public void TestMethod(EventHandler handler)
    {
        MyEvent += handler; // Event subscription is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithLockStatement_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly object _lock = new object();

    [EnforcePure]
    public int TestMethod(int x)
    {
        lock (_lock) // Lock statement is impure
        {
            return x * 2;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithUnsafeCode_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public unsafe void TestMethod(int* ptr)
    {
        *ptr = 42; // Modifying memory through pointer is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 24)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithStackallocAndSpan_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        Span<int> span = stackalloc int[10];
        for (int i = 0; i < 10; i++)
        {
            span[i] = i * i;
        }
        return span[5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithYieldReturn_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(int n)
    {
        for (int i = 0; i < n; i++)
        {
            yield return i * i;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithLocalFunction_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public int TestMethod(int x)
    {
        int LocalFunction()
        {
            _field++; // Impure operation in local function
            return x * 2;
        }

        return LocalFunction();
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithTupleDeconstruction_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public (int, int) TestMethod((int x, int y) input)
    {
        var (a, b) = input;
        return (a * 2, b * 3);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithPatternMatching_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(object obj)
    {
        return obj switch
        {
            int i => $""Integer: {i}"",
            string s => $""String: {s}"",
            _ => ""Unknown type""
        };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithNullCoalescing_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string input)
    {
        return input?.ToUpper() ?? ""EMPTY"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithStaticFieldAccess_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static int _counter;

    [EnforcePure]
    public void TestMethod()
    {
        _counter++;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithConstantFolding_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private const double PI = 3.14159;
    private const int FACTOR = 2;

    [EnforcePure]
    public double TestMethod(double radius)
    {
        const double area = PI * FACTOR;
        return radius * area;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task MethodWithRefParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(ref int value)
    {
        value = 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(10, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
