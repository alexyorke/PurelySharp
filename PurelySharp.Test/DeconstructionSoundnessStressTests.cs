using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DeconstructionSoundnessStressTests
    {
        [Test]
        public async Task DeconstructionDeclarationImpureDeconstruct_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Pair
{
    public void Deconstruct(out int left, out int right)
    {
        Console.WriteLine(""impure"");
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        var (left, right) = new Pair();
        return left + right;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DeconstructionDeclarationPureDeconstruct_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public struct Pair
{
    [PureExternal]
    public void Deconstruct(out int left, out int right)
    {
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        var (left, right) = new Pair();
        return left + right;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DeconstructionAssignmentImpureDeconstruct_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Pair
{
    public void Deconstruct(out int left, out int right)
    {
        Console.WriteLine(""impure"");
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        int left;
        int right;
        (left, right) = new Pair();
        return left + right;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtensionDeconstructionImpureDeconstruct_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Pair
{
}

public static class PairExtensions
{
    public static void Deconstruct(this Pair pair, out int left, out int right)
    {
        Console.WriteLine(""impure"");
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        var (left, right) = new Pair();
        return left + right;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PositionalPatternImpureDeconstruct_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Pair
{
    public void Deconstruct(out int left, out int right)
    {
        Console.WriteLine(""impure"");
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(Pair pair)
    {
        return pair is Pair(1, 2);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PositionalPatternPureDeconstruct_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public struct Pair
{
    [PureExternal]
    public void Deconstruct(out int left, out int right)
    {
        left = 1;
        right = 2;
    }
}

public sealed class TestClass
{
    [EnforcePure]
    public bool TestMethod(Pair pair)
    {
        return pair is Pair(1, 2);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
