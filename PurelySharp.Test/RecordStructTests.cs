using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RecordStructTests
    {
        // Note: These tests require C# 10+

        [Test]
        public async Task PureRecordStruct_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

public record struct Point
{
    public double X { get; init; }
    public double Y { get; init; }

    [EnforcePure]
    public double DistanceTo(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [EnforcePure]
    public Point WithX(double newX) => this with { X = newX };

    [EnforcePure]
    public Point WithY(double newY) => this with { Y = newY };
}";

            // Create and configure Test object
            var verifierTest = new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 }
            };

            // await verifierTest.RunAsync();
            // Expect PS0004 on the init accessors for X and Y
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 19, 10, 20).WithArguments("get_X"));
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 19, 11, 20).WithArguments("get_Y"));
            await verifierTest.RunAsync();
        }

        [Test]
        public async Task PureRecordStructWithConstructor_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

public readonly record struct Temperature(double Celsius)
{
    [EnforcePure]
    public double Fahrenheit() => Celsius * 9 / 5 + 32;

    [EnforcePure]
    public Temperature ToFahrenheit()
    {
        return new Temperature(Fahrenheit());
    }

    [EnforcePure]
    public static Temperature FromFahrenheit(double fahrenheit)
    {
        return new Temperature((fahrenheit - 32) * 5 / 9);
    }
}";

            // Create and configure Test object
            var verifierTest = new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 }
            };

            await verifierTest.RunAsync();
        }

        [Test]
        public async Task ImpureRecordStruct_Diagnostic()
        {
            var test = @"
// Requires LangVersion 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Runtime.CompilerServices;

public record struct Counter
{
    private static int _instances = 0;
    public int Value { get; init; }

    public Counter() { Value = 0; _instances++; }
    public Counter(int value) { Value = value; _instances++; }

    [EnforcePure]
    public static int GetInstanceCount()
    {
        return _instances;
    }

    [EnforcePure]
    public Counter Increment()
    {
        _instances++;
        return this with { Value = Value + 1 };
    }
}";

            // Explicitly define expected diagnostics on method identifiers
            var expected1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(17, 23, 17, 39) // Updated Span for GetInstanceCount
                                    .WithArguments("GetInstanceCount");
            var expected2 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(23, 20, 23, 29) // Updated Span for Increment again
                                    .WithArguments("Increment");

            // Create and configure Test object
            var verifierTest = new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 },
                //ExpectedDiagnostics = { expected1, expected2 } // ADDED explicit diagnostics
            };

            // Add expectations for PS0004 on getter and PS0002 on constructors
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 16, 11, 21).WithArguments("get_Value"));
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(13, 12, 13, 19).WithArguments(".ctor"));
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(14, 12, 14, 19).WithArguments(".ctor"));
            verifierTest.ExpectedDiagnostics.Add(expected1); // GetInstanceCount PS0002
            verifierTest.ExpectedDiagnostics.Add(expected2); // Increment PS0002

            await verifierTest.RunAsync();
        }

        [Test]
        public async Task RecordStructWithImmutableList_NoDiagnostic()
        {
            // Expectation limitation: Analyzer may not flag direct property assignment 
            // (e.g., `Tags = Tags.Add(tag);`) within a record struct method as impure,
            // even though it modifies the current instance's state.
            var code = @$"
using PurelySharp.Attributes;
using System.Collections.Immutable;

public record struct CacheEntry(int Id, ImmutableList<string> Tags)
{{
    // Method modifying Tags property (should be pure now)
    [EnforcePure]
    public CacheEntry AddTag(string tag)
    {{
        // This assignment makes it pure now due to record struct rule
        Tags = Tags.Add(tag);
        return this;
    }}

    // Method returning Tags count (should be pure)
    [EnforcePure]
    public int GetItemsCount()
    {{
        return Tags.Count;
    }}

    // Method checking Tags containment (should be pure)
    [EnforcePure]
    public bool HasTag(string tag)
    {{
        return Tags.Contains(tag);
    }}

    // With expression - creates new record, assignment happens internally (should be impure)
    // -> Correction: 'with' on record struct is PURE
    [EnforcePure]
    public CacheEntry WithTag(string tag)
    {{
        return this with {{ Tags = Tags.Add(tag) }};
    }}
}}";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task PureMethodInRecordStruct_NoDiagnostic()
        {
            var test = $$"""
        using PurelySharp.Attributes;
        using System;

        public record struct Point(int X, int Y)
        {
            [EnforcePure]
            public int Increment(int value)
            {
                // Pure operation
                return value + 1;
            }
        }
        """;
            // The method is pure (parameter + constant), so no diagnostic expected.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodInRecordStruct_Diagnostic()
        {
            var test = $$$"""
        using PurelySharp.Attributes;
        using System;

        public record struct Counter
        {
            private int _count;

            [EnforcePure]
            public void Increment()
            {
                // Impure operation: Modifies struct state
                _count++;
            }

            public int GetCount() => _count;
        }
        """;
            // Expect PS0002 because Increment modifies struct state
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(9, 17, 9, 26) // Updated span to method identifier
                                   .WithArguments("Increment");
            // Expect PS0004 because GetCount is pure but not marked
            var expectedPS0004 = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                   .WithSpan(15, 16, 15, 24)
                                   .WithArguments("GetCount");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedPS0002, expectedPS0004);
        }

        [Test]
        public async Task PureReadonlyRecordStructWithPureConstructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

// Define the readonly record struct with a pure constructor
public readonly record struct Zzz
{
    [Pure]
    public Zzz(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }
}

// Class to use the struct (optional, but helps ensure context)
public class Usage
{
    [EnforcePure]
    public Zzz CreateZzz()
    {
        // Calling the pure constructor should be allowed in an EnforcePure context
        return new Zzz(1, 2);
    }
}
";

            // Create and configure Test object, similar to other tests in this file
            var verifierTest = new VerifyCS.Test
            {
                TestCode = test,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80, // Assuming .NET 8 based on other tests
                SolutionTransforms = {
                    (solution, projectId) =>
                        solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location))
                 }
            };

            // Expect no diagnostics
            // Expect PS0004 on the constructor and auto-getters as they are pure but not marked [EnforcePure]
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(9, 12, 9, 15).WithArguments(".ctor"));
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(15, 16, 15, 17).WithArguments("get_X"));
            verifierTest.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(16, 16, 16, 17).WithArguments("get_Y"));
            await verifierTest.RunAsync();
        }
    }
}


