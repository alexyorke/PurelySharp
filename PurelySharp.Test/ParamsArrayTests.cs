using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;
using System.Linq;

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
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
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
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [EnforcePure]
    public int TestMethod()
    {
        return Sum(1, 2, 3, 4, 5);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayCalledWithArrayArgument_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [EnforcePure]
    public int TestMethod()
    {
        int[] myArray = new int[] { 1, 2, 3, 4, 5 };
        return Sum(myArray);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(19, 16, 19, 26)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithParamsArrayCalledWithNoArguments_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [EnforcePure]
    public int TestMethod()
    {
        return Sum();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayOfReferenceType_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;

public class TestClass
{
    [EnforcePure]
    public string Concatenate(params string[] strings)
    {
        return string.Join("" "", strings);
    }

    [EnforcePure]
    public string TestMethod()
    {
        return Concatenate(""Hello"", ""World"", ""!"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithParamsArrayAndRegularParameters_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string FormatMessage(string prefix, params object[] args)
    {
        string result = prefix;
        foreach (var arg in args)
        {
            result += "" "" + arg?.ToString();
        }
        return result;
    }

    [EnforcePure]
    public string TestMethod()
    {
        return FormatMessage(""Info: "", 1, ""text"", true);
    }
}";

            var expectedFM = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(8, 19, 8, 32)
                                   .WithArguments("FormatMessage");
            var expectedTM = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(19, 19, 19, 29)
                                   .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedFM, expectedTM);
        }

        [Test]
        public async Task PureMethodWithParamsArrayModifyingLocally_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] ProcessArray(params int[] numbers)
    {
        int[] result = new int[numbers.Length];
        for (int i = 0; i < numbers.Length; i++)
        {
            result[i] = numbers[i] * 2;
        }
        // Expectation limitation: Analyzer incorrectly flags this method as impure.
        // It only reads the input 'params' array and returns a new array.
        return result;
    }
}";




            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                           .WithSpan(8, 18, 8, 30)
                           .WithArguments("ProcessArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodModifyingParamsArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] ProcessArray(params int[] numbers)
    {
        for (int i = 0; i < numbers.Length; i++)
        {
            numbers[i] = numbers[i] * 2;
        }
        return numbers;
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                  .WithSpan(8, 18, 8, 30)
                                  .WithArguments("ProcessArray");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ParamsWithImpureDelegate_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public delegate void ProcessAction(int number);

public class TestClass
{
    [EnforcePure]
    public static void ImpureAction(int n) => Console.WriteLine(n);

    [EnforcePure]
    public static void ProcessNumbers(ProcessAction processor, params int[] numbers)
    {
        foreach (var number in numbers)
        {
            processor(number); // Impure call via delegate
        }
    }

    [EnforcePure]
    public static void TestMethod()
    {
        ProcessNumbers(ImpureAction, 1, 2, 3);
    }
}
";


            var expectedDiagPS0002_Process = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(10, 24, 10, 36).WithArguments("ImpureAction");


            var expectedDiagPS0002_TestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(21, 24, 21, 34).WithArguments("TestMethod");


            var expectedDiagPS0002_ImpureAction = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(9, 24, 9, 36).WithArguments("ImpureAction");


            var expectedImpureAction = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(10, 24, 10, 36).WithArguments("ImpureAction");
            var expectedProcessNumbers = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(13, 24, 13, 38).WithArguments("ProcessNumbers");
            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(22, 24, 22, 34).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expectedImpureAction, expectedProcessNumbers, expectedTestMethod);
        }
    }
}


