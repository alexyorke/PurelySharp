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
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Span<byte> TestMethod(Span<byte> initialSpan)
    {
        // Pure: Creates a new view/slice
        return initialSpan.Slice(1, 2);
    }
}";




            await VerifyCS.VerifyAnalyzerAsync(test);
        }













        [StructLayout(LayoutKind.Sequential)]
        public struct MyStruct { public int Value; }




    }
}