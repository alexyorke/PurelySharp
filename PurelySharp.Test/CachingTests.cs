using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
// using Microsoft.Extensions.Caching.Memory; // Requires Caching packages
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class CachingTests
    {
        // Note: These tests require adding Microsoft.Extensions.Caching.Memory package.
        // They serve as placeholders. All standard cache operations modifying or reading 
        // potentially time-expiring/evicted state are considered impure.

        // TODO: Add real tests if caching analysis becomes a priority.

        /*
        [Test]
        public async Task MemoryCache_Set_Diagnostic() // Example placeholder
        {
            var test = @"
#nullable enable
using System;
using Microsoft.Extensions.Caching.Memory;



public class TestClass
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [EnforcePure] 
    public void TestMethod(string key, object value)
    {
        _cache.Set(key, value); // Impure: Modifies cache state
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(16, 9, 16, 30).WithArguments("TestMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires Caching references
            Assert.Inconclusive("Caching test needs Caching references.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task MemoryCache_Get_Diagnostic() // Example placeholder
        {
             var test = @"
#nullable enable
using System;
using Microsoft.Extensions.Caching.Memory;



public class TestClass
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [EnforcePure] 
    public object? TestMethod(string key)
    {
        return _cache.Get(key); // Impure: Reads state affected by time/external factors
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(16, 16, 16, 31).WithArguments("TestMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires Caching references
            Assert.Inconclusive("Caching test needs Caching references.");
            await Task.CompletedTask;
        }
        
        [Test]
        public async Task MemoryCache_TryGetValue_Diagnostic() // Example placeholder
        {
             var test = @"
#nullable enable
using System;
using Microsoft.Extensions.Caching.Memory;



public class TestClass
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [EnforcePure] 
    public bool TestMethod(string key, out object? value)
    {
        // Impure: Reads state, potentially modifies 'value' (out param)
        return _cache.TryGetValue(key, out value); 
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(17, 16, 17, 50).WithArguments("TestMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires Caching references
            Assert.Inconclusive("Caching test needs Caching references.");
            await Task.CompletedTask;
        }

        [Test]
        public async Task MemoryCache_Remove_Diagnostic() // Example placeholder
        {
             var test = @"
#nullable enable
using System;
using Microsoft.Extensions.Caching.Memory;



public class TestClass
{
    private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [EnforcePure] 
    public void TestMethod(string key)
    {
        _cache.Remove(key); // Impure: Modifies cache state
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(16, 9, 16, 27).WithArguments("TestMethod");
            // await VerifyCS.VerifyAnalyzerAsync(test, expected); // Requires Caching references
            Assert.Inconclusive("Caching test needs Caching references.");
            await Task.CompletedTask;
        }
        */
    }
} 