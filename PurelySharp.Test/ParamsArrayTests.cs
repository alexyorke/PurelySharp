using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ParamsArrayTests
    {
        [Test]
        public async Task PureMethodWithParamsArray_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayCalledWithMultipleArguments_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [Pure]
    public int TestMethod()
    {
        return Sum(1, 2, 3, 4, 5); // Calling with multiple arguments
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayCalledWithArrayArgument_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [Pure]
    public int TestMethod()
    {
        int[] myArray = new int[] { 1, 2, 3, 4, 5 };
        return Sum(myArray); // Calling with array argument
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayCalledWithNoArguments_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [Pure]
    public int TestMethod()
    {
        return Sum(); // Calling with no arguments
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayOfReferenceType_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public string Concatenate(params string[] strings)
    {
        return string.Join("", "", strings);
    }

    [Pure]
    public string TestMethod()
    {
        return Concatenate(""Hello"", ""World"", ""!""); // Params of reference type
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayAndRegularParameters_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public string FormatMessage(string prefix, params object[] args)
    {
        string result = prefix;
        foreach (var arg in args)
        {
            result += "" "" + arg.ToString();
        }
        return result;
    }

    [Pure]
    public string TestMethod()
    {
        return FormatMessage(""Info:"", 1, ""text"", true); // Mixed regular and params parameters
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayModifyingLocally_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int[] ProcessArray(params int[] numbers)
    {
        // Creating a new array with modified values - this is pure because we don't modify the original
        int[] result = new int[numbers.Length];
        for (int i = 0; i < numbers.Length; i++)
        {
            result[i] = numbers[i] * 2;
        }
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodModifyingParamsArray_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int[] ProcessArray(params int[] numbers)
    {
        // This is impure because we're modifying the array that was passed in
        for (int i = 0; i < numbers.Length; i++)
        {
            numbers[i] = numbers[i] * 2; // Modifying the input array
        }
        return numbers;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithSpan(15, 24, 15, 25).WithArguments("ProcessArray"));
        }
    }
}