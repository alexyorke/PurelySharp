using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ListPatternSoundnessTests
    {
        [Test]
        public async Task ArrayListPattern_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public bool TestMethod(int[] values)
    {
        return values is [1, _, ..];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CustomListPatternImpureLength_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public static class GlobalState
{
    public static int Count;
}

public sealed class Sequence
{
    public int Length
    {
        get
        {
            GlobalState.Count++;
            return 2;
        }
    }

    public int this[int index] => index;
}

public sealed class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Sequence values)
    {
        return values is [0, 1];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CustomListPatternImpureIndexer_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public static class GlobalState
{
    public static int Count;
}

public sealed class Sequence
{
    public int Length => 2;

    public int this[int index]
    {
        get
        {
            GlobalState.Count++;
            return index;
        }
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Sequence values)
    {
        return values is [0, 1];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArraySlicePattern_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public sealed class TestClass
{
    [EnforcePure]
    public bool TestMethod(int[] values)
    {
        return values is [1, .. var tail] && tail.Length >= 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task CustomSlicePatternImpureSlice_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public static class GlobalState
{
    public static int Count;
}

public sealed class Sequence
{
    public int Length => 2;
    public int this[int index] => index;

    public Sequence Slice(int start, int length)
    {
        GlobalState.Count++;
        return this;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Sequence values)
    {
        return values is [0, .. var tail] && tail.Length >= 0;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
