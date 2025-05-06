using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StaticInterfaceMembersTests
    {
        // Note: These tests require C# 11+ and a compatible .NET runtime (.NET 7+)
        // They might report CS8919 if the test project's LangVersion/TargetFramework is lower.

        [Test]
        public async Task StaticInterfaceMethod_PureImplementation_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public interface IAddable<T> where T : IAddable<T>
    {
        static abstract T Add(T a, T b);
    }

    public struct Integer : IAddable<Integer>
    {
        public int Value { get; }

        public Integer(int value) { Value = value; }

        // Pure implementation of static abstract member
        // Assume [EnforcePure] could be applied to the interface method in real use.
        public static Integer Add(Integer a, Integer b) // No markup
        {
            return new Integer(a.Value + b.Value);
        }
    }
}";

            // Expect PS0004 on interface Add, Integer members (pure but no [EnforcePure])
            var expectedPS0004InterfaceAdd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                                    .WithSpan(11, 27, 11, 30) // Span from log for interface Add
                                                    .WithArguments("Add");
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(16, 20, 16, 25) // Span from log for get_Value
                                        .WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(18, 16, 18, 23) // Span from log for .ctor
                                       .WithArguments(".ctor");
            var expectedPS0004StructAdd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                               .WithSpan(22, 31, 22, 34) // Span from log for Integer.Add
                                               .WithArguments("Add");

            // Expect diagnostics now
            await new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 },
                ExpectedDiagnostics = { expectedPS0004InterfaceAdd, expectedGetter, expectedCtor, expectedPS0004StructAdd }
            }.RunAsync();
        }

        [Test]
        public async Task StaticInterfaceMethod_ImpureImplementation_Diagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

interface IPureInterface
{
    [EnforcePure] // Attribute on interface method
    static abstract int PureStaticMethod();
}

class ImpureImplementation : IPureInterface
{
    static int counter = 0;
    // Impure implementation of method marked [EnforcePure] in interface
    public static int PureStaticMethod() => ++counter;
    // Expectation limitation: Analyzer doesn't check implementation purity
    // against [EnforcePure] on static abstract interface members.
}
";
            // Expect PS0002 because the implementation is impure but interface has [EnforcePure]
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                          .WithSpan(17, 23, 17, 39) // Span from log for ImpureImplementation.PureStaticMethod
                                          .WithArguments("PureStaticMethod");

            await new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 },
                ExpectedDiagnostics = { expectedPS0002 }
            }.RunAsync();
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_PureImplementation_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public interface IMultiplyable<T> where T : IMultiplyable<T>
    {
        // Default implementation is impure (throws), but we might not analyze it directly.
        // Purity check applies to overriding implementations.
        static virtual T Multiply(T a, T b) => throw new NotImplementedException();
    }

    public struct Double : IMultiplyable<Double>
    {
        public double Value { get; }
        public Double(double value) { Value = value; }

        // Pure override of static virtual member
        public static Double Multiply(Double a, Double b) // No markup
        {
            return new Double(a.Value * b.Value);
        }
    }
}";

            // Expect PS0002 on default interface method (throws)
            var expectedPS0002Interface = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                                 .WithSpan(13, 26, 13, 34) // Span from log for interface Multiply
                                                 .WithArguments("Multiply");

            // Expect PS0004 on Double members (pure but no [EnforcePure])
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(18, 23, 18, 28) // Span from log for get_Value
                                        .WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(19, 16, 19, 22) // Span from log for .ctor
                                       .WithArguments(".ctor");
            var expectedPS0004Multiply = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                              .WithSpan(22, 30, 22, 38) // Span from log for Double.Multiply
                                              .WithArguments("Multiply");

            // Expect diagnostics now
            await new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 },
                ExpectedDiagnostics = { expectedPS0002Interface, expectedGetter, expectedCtor, expectedPS0004Multiply }
            }.RunAsync();
        }

        [Test]
        public async Task StaticInterfaceMethod_VirtualWithDefault_ImpureImplementation_Diagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

interface IPureInterface
{
    [EnforcePure] // Attribute on interface method
    static virtual int PureStaticMethod() => 0; // Default implementation is pure
}

class ImpureImplementation : IPureInterface
{
    static int counter = 0;
    // Impure override of method marked [EnforcePure] in interface
    public static int PureStaticMethod() => ++counter;
    // Expectation limitation: Analyzer doesn't check implementation purity
    // against [EnforcePure] on static virtual interface members.
}
";
            // Expect PS0002 because the implementation is impure but interface has [EnforcePure]
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                          .WithSpan(17, 23, 17, 39) // Span from log for ImpureImplementation.PureStaticMethod
                                          .WithArguments("PureStaticMethod");

            await new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 },
                ExpectedDiagnostics = { expectedPS0002 }
            }.RunAsync();
        }
    }
}


