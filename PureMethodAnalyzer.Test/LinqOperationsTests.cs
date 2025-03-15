using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestFixture]
    public class LinqOperationsTests
    {
        [Test]
        public async Task SimpleLinqQuery_NoDiagnostic()
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

        [Test]
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

        [Test]
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
    }
}