using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InlineArrayTests
    {




        [Test]
        public async Task ReadOnlyArray_IsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int ReadArray()
    {
        int[] buffer = new int[10];
        // Reading from an array is pure
        return buffer[5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task WritingToFreshLocalArray_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void WriteToArray()
    {
        int[] buffer = new int[10];
        buffer[5] = 42;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task InitializingFreshLocalArray_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int[] InitializeArray()
    {
        int[] buffer = new int[5];
        for (int i = 0; i < 5; i++)
        {
            buffer[i] = i * 2;
        }
        return buffer;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
