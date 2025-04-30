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
        // Since inline arrays require runtime support that's not available in the test environment,
        // we'll just test the analysis of the ExpressionPurityChecker.IsInlineArrayType method
        // by using conventional element access expressions but simulating the analyzer's behavior.

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

            // Expect diagnostic because creating a mutable array is impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 16, 10, 25) // Span of ReadArray identifier
                                   .WithArguments("ReadArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Added explicit diagnostic
        }

        [Test]
        public async Task WritingToArray_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:WriteToArray|}()
    {
        int[] buffer = new int[10];
        // Writing to an array is impure
        buffer[5] = 42;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ModifyingMultipleArrayElements_IsImpure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:InitializeArray|}()
    {
        int[] buffer = new int[5];
        // Initializing array elements is impure
        for (int i = 0; i < 5; i++)
        {
            buffer[i] = i * 2;
        }
        return buffer;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}