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
    public class IndexerTests
    {
        [Test]
        public async Task ReadingFromIndexer_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class CustomDictionary<TKey, TValue>
{
    private Dictionary<TKey, TValue> _innerDict = new Dictionary<TKey, TValue>();

    // Read-only indexer
    public TValue this[TKey key] => _innerDict[key];
}

public class TestClass
{
    private readonly CustomDictionary<string, int> _dictionary = new CustomDictionary<string, int>();

    [EnforcePure]
    public int GetValue(string key)
    {
        // Reading from an indexer should be pure
        return _dictionary[key];
    }
}";

            // Diagnostics are now inline - REMOVED explicit diagnostic
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WritingToIndexer_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class CustomDictionary<TKey, TValue>
{
    private Dictionary<TKey, TValue> _innerDict = new Dictionary<TKey, TValue>();

    // Indexer with getter and setter
    public TValue this[TKey key]
    {
        get => _innerDict[key];
        set => _innerDict[key] = value;
    }
}

public class TestClass
{
    private readonly CustomDictionary<string, int> _dictionary = new CustomDictionary<string, int>();

    [EnforcePure]
    public void {|PS0002:SetValue|}(string key, int value)
    {
        // Writing to an indexer with a setter should be impure
        _dictionary[key] = value;
    }
}";

            // Diagnostics are now inline - REMOVED explicit diagnostic
            // var expected2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                         .WithSpan(28, 17, 28, 25) // Location of SetValue
            //                         .WithArguments("SetValue");
            await VerifyCS.VerifyAnalyzerAsync(test); // REMOVED expected2 argument
        }

        [Test]
        public async Task ReadonlyIndexerProperty_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class ReadOnlyCollection<T>
{
    private readonly List<T> _items = new List<T>();

    // Read-only indexer (expression-bodied)
    public T this[int index] => _items[index];
}

public class TestClass
{
    private readonly ReadOnlyCollection<string> _collection = new ReadOnlyCollection<string>();

    [EnforcePure]
    public string GetItem(int index)
    {
        // Reading from a read-only indexer should be pure
        return _collection[index];
    }
}";

            // Diagnostics are now inline - REMOVED explicit diagnostic
            // var expected3 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                         .WithSpan(25, 19, 25, 26) // Location of GetItem
            //                         .WithArguments("GetItem");
            await VerifyCS.VerifyAnalyzerAsync(test); // REMOVED expected3 argument
        }

        [Test]
        //[Ignore("Temporarily disabled due to failure")]
        public async Task MixedAccessIndexer_ImpureWhenWriting()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class MixedAccessCollection<T>
{
    private readonly List<T> _items = new List<T>();

    // Indexer with getter and private setter
    public T this[int index]
    {
        [Pure]
        get => _items[index];
        private set => _items[index] = value; 
    }

    // Non-pure method that uses the private setter
    public void UpdateItem(int index, T value)
    {
        this[index] = value;
    }
}

public class TestClass
{
    private readonly MixedAccessCollection<string> _collection = new MixedAccessCollection<string>();

    [EnforcePure]
    public string GetItemPure(int index)
    {
        // Reading is pure via [Pure] getter
        return _collection[index];
    }

    [EnforcePure]
    public void {|PS0002:CallUpdateItemImpure|}(int index, string value)
    {
        // Calling a method that modifies state is impure
        _collection.UpdateItem(index, value);
    }
}";

            // Diagnostics are now inline (should only expect PS0002 on CallUpdateItemImpure) - REMOVED explicit diagnostic
            // var expected4 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
            //                         .WithSpan(35, 17, 35, 35) // Location of CallUpdateItemImpure
            //                         .WithArguments("CallUpdateItemImpure");
            await VerifyCS.VerifyAnalyzerAsync(test); // REMOVED expected4 argument
        }

        [Test]
        public async Task NestedIndexerAccess_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class NestedCollection
{
    private readonly Dictionary<string, Dictionary<int, string>> _nestedDict 
        = new Dictionary<string, Dictionary<int, string>>();

    // Nested indexer - first level
    public Dictionary<int, string> this[string key] => _nestedDict[key];
}

public class TestClass
{
    private readonly NestedCollection _collection = new NestedCollection();

    [EnforcePure]
    public string GetNestedValue(string outerKey, int innerKey)
    {
        // Nested indexer access should be pure
        return _collection[outerKey][innerKey];
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}