using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class YieldTests
    {
        [Test]
        public async Task PureMethodWithYield_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:GetNumbers|}()
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithYield_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    private int _state;

    [EnforcePure]
    public IEnumerable<int> {|PS0002:GetNumbers|}()
    {
        _state++; // Impure operation
        yield return _state;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithYieldAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> {|PS0002:GetNumbers|}()
    {
        Console.WriteLine(""Generating numbers""); // Impure operation
        yield return 1;
        yield return 2;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


