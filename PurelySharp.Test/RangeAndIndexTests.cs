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
    public class RangeAndIndexTests
    {
        [Test]
        public async Task FromEndIndex_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class C
{
    [EnforcePure]
    public int Last(int[] a)
    {
        return a[^1];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RangeSlice_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class C
{
    [EnforcePure]
    public int[] Tail(int[] a)
    {
        return a[1..];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RangeWithExpressions_PureWhenEndpointsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class C
{
    [EnforcePure]
    public int[] Middle(int[] a, int start, int len)
    {
        var s = start;
        var e = start + len;
        return a[s..e];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


