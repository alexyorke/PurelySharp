using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionViewTests
    {
        [Test]
        public async Task DictionaryKeys_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Dictionary<int, string>.KeyCollection {|PS0002:TestMethod|}(Dictionary<int, string> values)
    {
        return values.Keys;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DictionaryValues_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Dictionary<int, string>.ValueCollection {|PS0002:TestMethod|}(Dictionary<int, string> values)
    {
        return values.Values;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SortedDictionaryKeys_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public SortedDictionary<int, string>.KeyCollection {|PS0002:TestMethod|}(SortedDictionary<int, string> values)
    {
        return values.Keys;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SortedDictionaryValues_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public SortedDictionary<int, string>.ValueCollection {|PS0002:TestMethod|}(SortedDictionary<int, string> values)
    {
        return values.Values;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IDictionaryKeys_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ICollection<int> {|PS0002:TestMethod|}(IDictionary<int, string> values)
    {
        return values.Keys;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IDictionaryValues_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ICollection<string> {|PS0002:TestMethod|}(IDictionary<int, string> values)
    {
        return values.Values;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task QueueSynchronized_Diagnostic()
        {
            var test = @"
using System.Collections;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Queue {|PS0002:TestMethod|}(Queue values)
    {
        return Queue.Synchronized(values);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayListAdapter_Diagnostic()
        {
            var test = @"
using System.Collections;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ArrayList {|PS0002:TestMethod|}(IList values)
    {
        return ArrayList.Adapter(values);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListAsReadOnly_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ReadOnlyCollection<int> {|PS0002:TestMethod|}(List<int> values)
    {
        return values.AsReadOnly();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayAsReadOnly_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.ObjectModel;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ReadOnlyCollection<int> {|PS0002:TestMethod|}(int[] values)
    {
        return Array.AsReadOnly(values);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CollectionsMarshalAsSpan_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Span<int> {|PS0002:TestMethod|}(List<int> values)
    {
        return CollectionsMarshal.AsSpan(values);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
