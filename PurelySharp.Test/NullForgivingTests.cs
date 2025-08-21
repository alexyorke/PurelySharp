﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullForgivingTests
    {
        [Test]
        public async Task PureMethodWithNullForgiving_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(string input)
    {
        // Null forgiving operator is considered pure
        return input!.Length;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullForgiving_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(string input)
    {
        // Null forgiving with console write is impure
        Console.WriteLine(input!);
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(10, 17, 10, 27)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task PureMethodWithNullForgivingAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private int _field;

    [EnforcePure]
    public int TestMethod(string input)
    {
        // Null forgiving is pure, but field increment is impure
        var length = input!.Length;
        _field++;
        return length;
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(12, 16, 12, 26)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

    }
}


