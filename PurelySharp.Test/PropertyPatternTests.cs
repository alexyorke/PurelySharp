using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class PropertyPatternTests
    {
        [Test]
        public async Task PurePropertyPattern_NoDiagnostic()
        {
            var test = @"
#pragma warning disable PS0004
using PurelySharp.Attributes;

public sealed class Point
{
    [EnforcePure]
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(Point point)
    {
        return point is { X: 0, Y: 0 };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PropertyPatternWithImpureGetter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class Probe
{
    public int Value
    {
        get
        {
            Console.WriteLine(""reading"");
            return 1;
        }
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Probe probe)
    {
        return probe is { Value: 1 };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
