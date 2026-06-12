using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ComparisonDispatchTests
    {
        [Test]
        public async Task SortedDictionaryContainsKeyDispatchToImpureComparable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableKey : IComparable<MutableKey>
{
    public int CompareTo(MutableKey other)
    {
        Console.WriteLine(""compare"");
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(SortedDictionary<MutableKey, int> values, MutableKey key)
    {
        return values.ContainsKey(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SortedDictionaryTryGetValueDispatchToImpureComparable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableKey : IComparable<MutableKey>
{
    public int CompareTo(MutableKey other)
    {
        Console.WriteLine(""compare"");
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(SortedDictionary<MutableKey, int> values, MutableKey key)
    {
        return values.TryGetValue(key, out var result) && result > 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SortedDictionaryContainsKeyForBuiltinKey_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(SortedDictionary<int, string> values, int key)
    {
        return values.ContainsKey(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListBinarySearchDispatchToImpureComparable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableKey : IComparable<MutableKey>
{
    public int CompareTo(MutableKey other)
    {
        Console.WriteLine(""compare"");
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(List<MutableKey> values, MutableKey key)
    {
        return values.BinarySearch(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListBinarySearchForBuiltinKey_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(List<int> values, int key)
    {
        return values.BinarySearch(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SpanBinarySearchDispatchToImpureComparable_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class MutableKey : IComparable<MutableKey>
{
    public int CompareTo(MutableKey other)
    {
        Console.WriteLine(""compare"");
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(ReadOnlySpan<MutableKey> values, MutableKey key)
    {
        return values.BinarySearch(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SpanBinarySearchForBuiltinKey_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(ReadOnlySpan<int> values, int key)
    {
        return values.BinarySearch(key);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
