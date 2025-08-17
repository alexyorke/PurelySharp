﻿using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LambdaTests
    {
        [Test]
        public async Task PureMethodWithLambda_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(List<int> list)
    {
        // Lambda expression itself is pure, Select is pure.
        // Analyzer now seems to handle this correctly.
        return list.Select(x => x * 2);
    }
}
";


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithLambda_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public void TestMethod(int[] numbers)
    {
        // Lambda that performs an impure operation (Console.WriteLine)
        numbers.ToList().ForEach(x => Console.WriteLine(x));
    }
}";


            var expected1 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                    .WithSpan(11, 17, 11, 27)
                                    .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected1);
        }

        [Test]
        public async Task PureMethodWithLambdaCapture_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    private int _sum;

    [EnforcePure]
    public int[] TestMethod/*TestMethod*/(int[] numbers)
    {
        // Lambda that captures and modifies a field (impure)
        numbers.ToList().ForEach(x => _sum += x);
        return numbers;
    }
}";


            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(13, 18, 13, 28)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


