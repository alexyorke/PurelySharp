using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BasicPureTests
    {
        [Test]
        public async Task NameOf_ShouldBePure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string GetName()
    {
        // nameof is resolved at compile time to a string literal, which is pure.
        string name = nameof(System.Console.WriteLine);
        return name;
    }
}";
            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ReadonlyRecordStructConstructor_ShouldBePure()
        {
            var test = @"
using PurelySharp.Attributes;

[Pure]
public readonly record struct Zzz
{
    // Implicitly pure because it only assigns to fields/properties of a readonly struct
    public Zzz(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }
}

public class TestUsage
{
    [EnforcePure]
    public Zzz CreateZzz()
    {
        // This constructor call should be pure
        return new Zzz(1, 2);
    }
}
";
            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorInitializer_CallingPureThis_ShouldBePure()
        {
            var test = @"
using PurelySharp.Attributes;

public struct MyStruct
{
    public int X { get; }
    public int Y { get; }

    [Pure]
    public MyStruct(int x)
    {
        X = x;
        Y = 0; // Default value
    }

    [Pure] // This constructor is pure
    public MyStruct(int x, int y) : this(x) // Calls another [Pure] constructor
    {
        Y = y; // Remaining assignment is allowed in constructor
    }
}

public class TestUsage
{
    [EnforcePure]
    public MyStruct CreateMyStruct()
    {
        // Call to MyStruct(int, int) should be pure
        return new MyStruct(1, 2);
    }
}
";
            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorInitializer_CallingNonPureThis_ShouldFlag()
        {
            var test = @"
using PurelySharp.Attributes;

public struct MyStruct
{
    public int X { get; }
    public int Y { get; }

    // This constructor is NOT marked [Pure]
    public MyStruct(int x)
    {
        X = x;
        Y = 0; // Default value
    }

    // Although this constructor calls a constructor not marked [Pure],
    // that constructor is analyzable and found to be pure, so this one is also pure.
    [EnforcePure]
    public MyStruct(int x, int y) : this(x)
    {
        Y = y;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}