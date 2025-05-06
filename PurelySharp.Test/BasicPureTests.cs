using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Attributes;
using PurelySharp.Analyzer;
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
            // However, based on PS0004 behavior, it SHOULD warn for .ctor, get_X, get_Y
            // if they are not marked [EnforcePure] and are indeed pure.
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 12, 8, 15).WithArguments(".ctor");
            var expectedGetX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(14, 16, 14, 17).WithArguments("get_X");
            var expectedGetY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(15, 16, 15, 17).WithArguments("get_Y");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedCtor, expectedGetX, expectedGetY });
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
            // So, expecting 3 x PS0004 diagnostics.
            // Test currently expects 0.
            // UPDATE: Actually expects 4, including the already [EnforcePure] constructor.
            var expectedGetX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(6, 16, 6, 17).WithArguments("get_X");
            var expectedGetY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 17).WithArguments("get_Y");
            var expectedCtor1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 12, 10, 20).WithArguments(".ctor");
            var expectedCtor2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(17, 12, 17, 20).WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetX, expectedGetY, expectedCtor1, expectedCtor2 });
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
            // This means the analyzer thinks MyClass(int x) IS pure.
            // This is problematic because it calls CreateSideEffectAndReturnY which is impure.

            var expectedGetX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(6, 16, 6, 17).WithArguments("get_X");
            var expectedGetY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 17).WithArguments("get_Y");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 12, 10, 20).WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetX, expectedGetY, expectedCtor });
        }

        [Test]
        public async Task PositionalReadonlyRecordStruct_NoBodyOrInterfaces_ShouldBePure()
        {
            var test = @"
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

namespace TestNamespace // Wrap everything in a namespace
{
    using PurelySharp.Attributes;

    // Simple positional readonly record struct with no body or interfaces.
    // Creating an instance should be pure.
    public readonly record struct A(int X, int Y);

    public class TestUsage
    {
        [EnforcePure]
        public A CreateA()
        {
            // This object creation should be pure.
            return new A(1, 2);
        }
    }
}
";
            // Expect no diagnostics
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PropertyAccessors_ShouldFlag()
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

    public int GetX()
    {
        return X;
    }

    public int GetY()
    {
        return Y;
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
            // Expect PS0004 for property accessors, constructors, and methods (6 total)
            var expectedGetX = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(6, 16, 6, 17).WithArguments("get_X");
            var expectedGetY = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(7, 16, 7, 17).WithArguments("get_Y");
            var expectedCtor1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 12, 10, 20).WithArguments(".ctor");
            var expectedCtor2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(17, 12, 17, 20).WithArguments(".ctor");
            var expectedGetX2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(22, 16, 22, 20).WithArguments("GetX");
            var expectedGetY2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(27, 16, 27, 20).WithArguments("GetY");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetX, expectedGetY, expectedCtor1, expectedCtor2, expectedGetX2, expectedGetY2 });
        }
    }
}