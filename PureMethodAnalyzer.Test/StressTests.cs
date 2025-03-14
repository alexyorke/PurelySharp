using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class StressTests
    {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public async Task MethodWithComplexSideEffects_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static int counter;
    private readonly List<int> cache = new();

    [EnforcePure]
    public IEnumerable<int> TestMethod<T>(
        IEnumerable<T> source, 
        Func<T, int> selector)
    {
        foreach (var item in source)
        {
            var value = selector(item);
            if (value % 2 == 0)
            {
                cache.Add(value); // Side effect: modifying instance state
                counter++; // Side effect: modifying static state
                yield return value;
            }
            else
            {
                var temp = new List<int> { value };
                yield return temp.First(); // Pure operation
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 29)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithNestedClosures_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int state;

    [EnforcePure]
    public Func<int, int> TestMethod(int multiplier)
    {
        int LocalFunction(int x)
        {
            state++; // Side effect in local function
            return x * multiplier;
        }

        return x =>
        {
            var result = LocalFunction(x);
            state += result; // Side effect in lambda
            return result;
        };
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 27)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task MethodWithMixedPureAndImpureOperations_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly Dictionary<int, int> cache = new();

    [EnforcePure]
    public int TestMethod(IEnumerable<int> numbers)
    {
        // Pure operations
        var result = numbers
            .Where(x => x > 0)
            .Select(x => x * x)
            .OrderBy(x => x)
            .Take(5)
            .Sum();

        // Impure operation mixed in
        if (!cache.ContainsKey(result))
        {
            cache[result] = result; // Side effect
        }

        // More pure operations
        return (int)Math.Sqrt(result);
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 16)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}