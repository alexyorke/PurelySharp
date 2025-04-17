using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Span<byte> TestMethod(byte[] data)
    {
        // Pure: Creates a view over existing memory
        return new Span<byte>(data);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Span_Slice_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Span<byte> TestMethod(Span<byte> initialSpan)
    {
        // Pure: Creates a new view/slice
        return initialSpan.Slice(1, 2);
    }
}";
            // Expect PMA0002 because span.Slice is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(14, 16, 14, 39)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // --- Span<T> / Memory<T> Modification (Impure) ---
        // TODO: Enable test once analyzer flags Span indexing assignment as impure
        /*
        [Test]
        public async Task Span_IndexAssignment_Diagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Span<int> dataSpan)
    {
        dataSpan[0] = 123; // Impure: Modifies underlying memory
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 25).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- stackalloc (Impure) ---
        // TODO: Enable test once analyzer flags stackalloc as impure
        /*
        [Test]
        public async Task StackAlloc_Diagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public unsafe class TestClass // Requires unsafe context
{
    [EnforcePure]
    public int TestMethod()
    {
        Span<int> numbers = stackalloc int[10]; // Impure: Stack allocation
        numbers[0] = 1;
        return numbers[0];
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 29, 14, 48).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Marshal operations (Impure) ---
        // TODO: Enable tests below once analyzer flags Marshal methods as impure

        /*
        [Test]
        public async Task Marshal_AllocFreeHGlobal_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        IntPtr ptr = Marshal.AllocHGlobal(100); // Impure: Unmanaged allocation
        Marshal.FreeHGlobal(ptr); // Impure: Unmanaged free
    }
}";
            // Expect diagnostic on first impure call (AllocHGlobal)
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 22, 14, 49).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MyStruct { public int Value; }

        [Test]
        public async Task Marshal_StructureToPtr_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

[StructLayout(LayoutKind.Sequential)]
public struct MyStruct { public int Value; }

public unsafe class TestClass // Add unsafe here
{
    [EnforcePure]
    public void TestMethod(IntPtr ptr, MyStruct s)
    {
        Marshal.StructureToPtr(s, ptr, false); // Impure: Writes to unmanaged memory
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(18, 9, 18, 46).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        
        [Test]
        public async Task Marshal_PtrToStructure_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Runtime.InteropServices;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

[StructLayout(LayoutKind.Sequential)]
public struct MyStruct { public int Value; }

public unsafe class TestClass // Add unsafe here
{
    [EnforcePure]
    public MyStruct TestMethod(IntPtr ptr)
    {
        return Marshal.PtrToStructure<MyStruct>(ptr); // Impure: Reads from unmanaged memory
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(18, 16, 18, 56).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- GC Operations (Impure) ---
        // TODO: Enable test once analyzer flags GC.Collect as impure
        /*
        [Test]
        public async Task GC_Collect_Diagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        GC.Collect(); // Impure: Affects process state non-deterministically
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(13, 9, 13, 21).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 