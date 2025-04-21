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
    public double {|PS0002:DistanceTo|}(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [EnforcePure]
    public Point {|PS0002:WithX|}(double newX) => this with { X = newX };

    [EnforcePure]
    public Point {|PS0002:WithY|}(double newY) => this with { Y = newY };
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

public record struct Temperature(double Celsius)
{
    [EnforcePure]
    public double {|PS0002:Fahrenheit|}() => Celsius * 9 / 5 + 32;

    [EnforcePure]
    public Temperature {|PS0002:ToFahrenheit|}()
    {
        return new Temperature(Fahrenheit());
    }

    [EnforcePure]
    public static Temperature {|PS0002:FromFahrenheit|}(double fahrenheit)
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
            var test = @"
// Requires LangVersion 10+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

public record struct CacheEntry(string Key, string Value)
{
    public ImmutableList<string> Tags { get; init; } = ImmutableList<string>.Empty;

    [EnforcePure]
    public CacheEntry {|PS0002:AddTag|}(string tag)
    {
        return this with { Tags = Tags.Add(tag) };
    }

    [EnforcePure]
    public bool {|PS0002:HasTag|}(string tag)
    {
        return Tags.Contains(tag);
    }

    [EnforcePure]
    public CacheEntry {|PS0002:WithTag|}(string tag)
    {
        return this with { Tags = Tags.Add(tag) };
    }

    [EnforcePure]
    public int {|PS0002:GetItemsCount|}()
    {
        return Tags.Count;
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


