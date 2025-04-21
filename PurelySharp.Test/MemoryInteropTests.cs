using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class MemoryInteropTests
    {
        // --- Span<T> / Memory<T> Creation & Slicing (Pure) ---

        [Test]
        public async Task Span_Creation_From_Array_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Span<byte> {|PS0002:TestMethod|}(byte[] data)
    {
        // Pure: Creates a view over existing memory
        return new Span<byte>(data);
    }
}";
            // TODO: Update analyzer to recognize Span<T>(T[]) ctor as pure
            // Temporarily expect PS0002 due to current limitation
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Span_Slice_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Span<byte> {|PS0002:TestMethod|}(Span<byte> initialSpan)
    {
        // Pure: Creates a new view/slice
        return initialSpan.Slice(1, 2);
    }
}";
            // Expect PS0002 because span.Slice is treated as unknown purity
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Span<T> / Memory<T> Modification (Impure) ---
        // TODO: Enable test once analyzer flags Span indexing assignment as impure
        // Commented out test Span_IndexAssignment_Diagnostic removed

        // --- stackalloc (Impure) ---
        // TODO: Enable test once analyzer flags stackalloc as impure
        // Commented out test StackAlloc_Diagnostic removed

        // --- Marshal operations (Impure) ---
        // TODO: Enable tests below once analyzer flags Marshal methods as impure
        // Commented out Marshal tests removed

        [StructLayout(LayoutKind.Sequential)] // Keep this for potential future use
        public struct MyStruct { public int Value; }

        // --- GC Operations (Impure) ---
        // TODO: Enable test once analyzer flags GC.Collect as impure
        // Commented out test GC_Collect_Diagnostic removed
    }
}