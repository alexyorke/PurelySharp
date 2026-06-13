using NUnit.Framework;
using System.Threading.Tasks;

using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test;

[TestFixture]
public class SpanMutationTests
{
    [Test]
    public async Task SpanClear_Diagnostic()
    {
        var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<int> values)
    {
        values.Clear();
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task SpanFill_Diagnostic()
    {
        var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<int> values)
    {
        values.Fill(42);
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task SpanCopyTo_Diagnostic()
    {
        var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<int> source, Span<int> destination)
    {
        source.CopyTo(destination);
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task SpanTryCopyTo_Diagnostic()
    {
        var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Span<int> source, Span<int> destination)
    {
        return source.TryCopyTo(destination);
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [Test]
    public async Task SpanReverse_Diagnostic()
    {
        var test = @"
using System;
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(Span<int> values)
    {
        values.Reverse();
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
