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
    public class LinqOperationsTests
    {
        [Test]
        public async Task SimpleLinqQuery_NoDiagnostic()
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
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



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
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;



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

        [Test]
        public async Task LinqSourceWithImpureGetEnumerator_Diagnostic()
        {
            var test = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class ImpureSequence : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        Console.WriteLine(""enumerating"");
        return Enumerable.Empty<int>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(ImpureSequence numbers)
    {
        return numbers.Select(x => x);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqSourceWithImpureExplicitGetEnumerator_Diagnostic()
        {
            var test = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class ExplicitImpureSequence : IEnumerable<int>
{
    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        Console.WriteLine(""enumerating"");
        return Enumerable.Empty<int>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<int>)this).GetEnumerator();
    }
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(ExplicitImpureSequence numbers)
    {
        return numbers.Select(x => x);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctWithImpureEqualityComparer_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class ImpureComparer : IEqualityComparer<int>
{
    public bool Equals(int x, int y)
    {
        Console.WriteLine(""comparing"");
        return x == y;
    }

    public int GetHashCode(int obj) => obj;
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(IEnumerable<int> numbers)
    {
        return numbers.Distinct(new ImpureComparer());
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctWithPureEqualityComparer_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class PureComparer : IEqualityComparer<int>
{
    public bool Equals(int x, int y) => x == y;

    public int GetHashCode(int obj) => obj;
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> numbers)
    {
        return numbers.Distinct(new PureComparer());
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctDefaultEqualityDispatchToImpureEquatable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<MutableRecord> {|PS0002:TestMethod|}(IEnumerable<MutableRecord> values)
    {
        return values.Distinct();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctDefaultEqualityForBuiltinValue_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> values)
    {
        return values.Distinct();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctNullComparerDispatchToImpureEquatable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<MutableRecord> {|PS0002:TestMethod|}(IEnumerable<MutableRecord> values)
    {
        return values.Distinct(null);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctDefaultComparerForBuiltinValue_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> values)
    {
        return values.Distinct(default(IEqualityComparer<int>));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqDistinctWithInterfaceEqualityComparerParameter_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(IEnumerable<int> numbers, IEqualityComparer<int> comparer)
    {
        return numbers.Distinct(comparer);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqOrderByWithImpureComparer_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class ImpureComparer : IComparer<int>
{
    public int Compare(int x, int y)
    {
        Console.WriteLine(""comparing"");
        return x.CompareTo(y);
    }
}

public class TestClass
{
    [EnforcePure]
    public IOrderedEnumerable<int> {|PS0002:TestMethod|}(IEnumerable<int> numbers)
    {
        return numbers.OrderBy(value => value, new ImpureComparer());
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqOrderByWithInterfaceComparerParameter_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IOrderedEnumerable<int> {|PS0002:TestMethod|}(IEnumerable<int> numbers, IComparer<int> comparer)
    {
        return numbers.OrderBy(value => value, comparer);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqSecondarySourceWithImpureGetEnumerator_Diagnostic()
        {
            var test = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class ImpureSequence : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        Console.WriteLine(""enumerating"");
        return Enumerable.Empty<int>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:TestMethod|}(IEnumerable<int> left, ImpureSequence right)
    {
        return left.Concat(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqSecondarySourceWithInterfaceEnumerable_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> left, IEnumerable<int> right)
    {
        return left.Concat(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


