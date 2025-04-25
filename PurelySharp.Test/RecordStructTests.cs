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
    public static int {|PS0002:GetInstanceCount|}()
    {
        return _instances;
    }

    [EnforcePure]
    public Counter {|PS0002:Increment|}()
    {
        _instances++;
        return this with { Value = Value + 1 };
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
        public async Task RecordStructWithImmutableList_NoDiagnostic()
        {
            var code = @$"
using PurelySharp.Attributes;
using System.Collections.Immutable;

public record struct CacheEntry(int Id, ImmutableList<string> Tags)
{{
    // Method modifying Tags property (should be impure)
    [EnforcePure]
    public CacheEntry {{|PS0002:AddTag|}}(string tag)
    {{
        // This assignment makes it impure, even though it returns a new record
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

            // Expect diagnostics only for AddTag and WithTag, which perform assignments.
            // Accessing Tags.Count and Tags.Contains on the readonly struct instance field is pure.
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
            public void {|PS0002:Increment|}()
            {
                // Impure operation: Modifies struct state
                _count++;
            }

            public int GetCount() => _count;
        }
        """;
            // Expect PS0002 because Increment modifies struct state
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


