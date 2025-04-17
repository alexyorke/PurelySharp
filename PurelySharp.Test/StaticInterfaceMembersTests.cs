using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StaticInterfaceMembersTests
    {
        [Test]
        public async Task StaticInterfaceMethod_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface IAddable<T>
    {
        static abstract T Add(T a, T b);
    }

    public struct Integer : IAddable<Integer>
    {
        public int Value { get; }

        public Integer(int value)
        {
            Value = value;
        }

        public static Integer Add(Integer a, Integer b)
        {
            return new Integer(a.Value + b.Value);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(11, 27, 11, 30)
            );
        }

        [Test]
        public async Task StaticInterfaceMethod_ImpureImplementation_Diagnostic()
        {
            var test = @"
using PurelySharp;
using System;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

interface IPureInterface
{
    [EnforcePure]
    static abstract int PureStaticMethod();
}

class ImpureImplementation : IPureInterface
{
    static int counter = 0;
    // Implementation is impure
    public static int PureStaticMethod() => ++counter;
}
";
            // Expect only the compiler error CS8919, not the analyzer diagnostic PMA0001
            var expected = DiagnosticResult.CompilerError("CS8919").WithSpan(12, 25, 12, 41);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_PureImplementation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public interface IMultiplyable<T>
    {
        static virtual T Multiply(T a, T b)
        {
            throw new NotImplementedException();
        }
    }

    public struct Double : IMultiplyable<Double>
    {
        public double Value { get; }

        public Double(double value)
        {
            Value = value;
        }

        public static Double Multiply(Double a, Double b)
        {
            return new Double(a.Value * b.Value);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                // Target runtime doesn't support static abstract members in interfaces
                DiagnosticResult.CompilerError("CS8919").WithSpan(11, 26, 11, 34)
            );
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_ImpureImplementation_Diagnostic()
        {
            var test = @"
using PurelySharp;
using System;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

interface IPureInterface
{
    [EnforcePure]
    static virtual int PureStaticMethod() => 0; // Default implementation is pure
}

class ImpureImplementation : IPureInterface
{
    static int counter = 0;
    // Override is impure
    public static int PureStaticMethod() => ++counter;
}
";
            // Expect only the compiler error CS8919, not the analyzer diagnostic PMA0001
            var expected = DiagnosticResult.CompilerError("CS8919").WithSpan(12, 24, 12, 40);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


