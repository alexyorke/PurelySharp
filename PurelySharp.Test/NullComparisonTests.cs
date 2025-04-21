using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullComparisonTests
    {
        [Test]
        public async Task PureMethodWithNullComparison_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(object obj)
    {
        // Null comparison is considered pure
        return obj == null;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithNullComparison_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(object obj)
    {
        // Null comparison with console write is impure
        if (obj == null)
        {
            Console.WriteLine(""Object is null"");
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithNullComparisonAndImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using PurelySharp.Attributes;

public class TestClass
{
    private int _field;

    [EnforcePure]
    public bool {|PS0002:TestMethod|}(object obj)
    {
        // Null comparison is pure, but field increment is impure
        bool isNull = obj == null;
        _field++;
        return isNull;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


