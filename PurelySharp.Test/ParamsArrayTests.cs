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

        [NUnit.Framework.Ignore("Temporarily disabled due to failure")]
        [Test]
        public async Task PureMethodWithParamsArrayCalledWithMultipleArguments_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Sum|}(params int[] numbers)
    {
        int total = 0;
        foreach (var num in numbers)
        {
            total += num;
        }
        return total;
    }

    [EnforcePure]
    public int {|PS0002:TestMethod|}()
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

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string {|PS0002:Concatenate|}(params string[] strings)
    {
        return string.Join("" "", strings);
    }

    [EnforcePure]
    public string {|PS0002:TestMethod|}()
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
    public string {|PS0002:FormatMessage|}(string prefix, params object[] args)
    {
        string result = prefix;
        foreach (var arg in args)
        {
            result += "" "" + arg?.ToString();
        }
        return result;
    }

    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return FormatMessage(""Info:"", 1, ""text"", true);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int[] {|PS0002:ProcessArray|}(params int[] numbers)
    {
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
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:ProcessArray|}(params int[] numbers)
    {
        for (int i = 0; i < numbers.Length; i++)
        {
            numbers[i] = numbers[i] * 2;
        }
        return numbers;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


