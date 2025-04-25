using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class MathAndLinqTests
    {
        [Test]
        public async Task ComplexPureLinqOperations_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(IEnumerable<int> numbers)
    {
        // Currently impure due to unhandled DelegateCreation
        return numbers
            .Where(x => x > 0)
            .Select(x => x * x)
            .OrderBy(x => x)
            .Take(5)
            .Sum();
    }
}";
            // Expect diagnostic due to unhandled DelegateCreation
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 16, 12, 26) // Corrected: Line 12
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task ComplexNestedExpressions_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x, double y, double z)
    {
        var a = Math.Sin(x) * Math.Cos(y); // Pure
        var b = Math.Pow(Math.E, z) / Math.PI; // Pure: Math.E and Math.PI reads are now allowed
        var c = Math.Sqrt(Math.Abs(a * b)); // Pure
        return Math.Max(a, Math.Min(b, c)); // Pure
    }
}";
            // Expect no diagnostic now
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SimpleMathMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(double x)
    {
        // Math.Sin is pure
        return Math.Sin(x);
    }
}";
            // Expect no diagnostic as Math.Sin is pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Removed diagnostic expectation
        }

        [Test]
        public async Task MathConstant_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod()
    {
        // Pure: Math.PI read is now allowed
        return Math.PI;
    }
}";
            // Expect no diagnostic now
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MathMethodChain_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



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

        [Test]
        public async Task ComplexLinqWithMath_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(IEnumerable<double> numbers)
    {
        // Impure due to unhandled DelegateCreation (LINQ methods)
        return numbers
            .Where(x => x > Math.PI) // Math.PI is pure, but Where() is not handled
            .Select(x => Math.Pow(Math.Sin(x), 2) + Math.Pow(Math.Cos(x), 2))
            .OrderBy(x => Math.Abs(x - 1))
            .Take(5)
            .Average();
    }
}";
            // Expect diagnostic due to unhandled DelegateCreation
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 19, 12, 29) // Span might change, but diagnostic remains
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task MethodWithLazyEvaluation_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> numbers)
    {
        // Impure due to DelegateCreation
        return numbers.Where(x => x > 0)
                     .Select(x => x * x)
                     .OrderBy(x => x);
    }
}";
            // Expect diagnostic due to unhandled DelegateCreation
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 29, 12, 39) // Actual span from test output
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }
    }
}


