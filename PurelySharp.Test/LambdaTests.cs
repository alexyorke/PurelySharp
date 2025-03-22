using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LambdaTests
    {
        [Test]
        public async Task PureMethodWithLambda_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int[] TestMethod(int[] numbers)
    {
        // Lambda that performs a pure operation
        return numbers.Select(x => x * 2).ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithLambda_Diagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(int[] numbers)
    {
        // Lambda that performs an impure operation (Console.WriteLine)
        numbers.ToList().ForEach(x => Console.WriteLine(x));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(14, 39, 14, 59).WithArguments("TestMethod"));
        }

        [Test]
        public async Task PureMethodWithLambdaCapture_Diagnostic()
        {
            var test = @"
using System;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _sum;

    [EnforcePure]
    public int[] TestMethod(int[] numbers)
    {
        // Lambda that captures and modifies a field (impure)
        numbers.ToList().ForEach(x => _sum += x);
        return numbers;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(16, 34, 16, 48).WithArguments("TestMethod"));
        }
    }
}


