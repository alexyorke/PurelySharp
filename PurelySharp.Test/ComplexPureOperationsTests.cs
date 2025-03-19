using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharp>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ComplexPureOperationsTests
    {
        [Test]
        public async Task DeepRecursiveMethodWithComplexLogic_NoDiagnostic()
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
    public int TestMethod(int n, IEnumerable<int> numbers)
    {
        if (n <= 0) return numbers.Sum();
        
        int LocalFunction(int x)
        {
            return x <= 1 ? 1 : LocalFunction(x - 1) + LocalFunction(x - 2);
        }

        var filtered = numbers
            .Where(x => x > LocalFunction(5))
            .Select(x => x * x)
            .OrderBy(x => Math.Abs(x))
            .Take(n)
            .ToList();

        return filtered.Any() 
            ? TestMethod(n - 1, filtered.Skip(1)) 
            : 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NestedTupleDeconstructionWithPatternMatching_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public (int sum, int count) TestMethod(IEnumerable<(int x, int y)> points)
    {
        static int Square(int n) => n * n;

        var result = points
            .Select(p => (
                distance: Math.Sqrt(Square(p.x) + Square(p.y)),
                point: p))
            .Where(t => t.distance > 0)
            .Aggregate(
                (sum: 0, count: 0),
                (acc, curr) => curr switch
                {
                    var (d, (x, y)) when x > 0 && y > 0 => (acc.sum + (int)d, acc.count + 1),
                    var (_, (x, _)) when x < 0 => acc,
                    _ => (acc.sum, acc.count)
                });

        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ComplexGenericConstraints_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public interface IValue<T> { T Value { get; } }
public interface IConverter<in TSource, out TResult> { TResult Convert(TSource source); }

public class TestClass
{
    [EnforcePure]
    public TResult TestMethod<TSource, TMiddle, TResult>(
        IEnumerable<IValue<TSource>> sources,
        IConverter<TSource, TMiddle> firstConverter,
        IConverter<TMiddle, TResult> secondConverter)
        where TSource : struct
        where TMiddle : class
        where TResult : struct
    {
        return sources
            .Select(s => s.Value)
            .Select(firstConverter.Convert)
            .Where(m => m != null)
            .Select(secondConverter.Convert)
            .Aggregate((x, y) => 
                Convert.ToInt32(x) > Convert.ToInt32(y) ? x : y);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}