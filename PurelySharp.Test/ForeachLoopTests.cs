using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ForeachLoopTests
    {
        [Test]
        public async Task ForeachImpureCollectionExpression_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        foreach (var value in GetValues())
        {
        }
    }

    private IEnumerable<int> GetValues()
    {
        Console.WriteLine(""loading"");
        return Array.Empty<int>();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ForeachImpureGetEnumerator_Diagnostic()
        {
            var test = @"
using System;
using System.Collections;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class ImpureSequence : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        Console.WriteLine(""enumerating"");
        return ((IEnumerable<int>)Array.Empty<int>()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(ImpureSequence values)
    {
        foreach (var value in values)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AwaitForeachImpureGetAsyncEnumerator_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PurelySharp.Attributes;

public static class GlobalState
{
    public static int Count;
}

public sealed class ImpureAsyncSequence : IAsyncEnumerable<int>
{
    public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        GlobalState.Count++;
        return new Enumerator();
    }

    private sealed class Enumerator : IAsyncEnumerator<int>
    {
        public int Current => 1;
        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
        public ValueTask DisposeAsync() => default;
    }
}

public class TestClass
{
    [EnforcePure]
    public async Task {|PS0002:TestMethod|}(ImpureAsyncSequence values)
    {
        await foreach (var value in values)
        {
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
