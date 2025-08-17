using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class RegexTests
    {


        [Test]
        public async Task Regex_IsMatch_Static_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text.RegularExpressions;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(string input)
    {
        // Static Regex.IsMatch is now known pure
        return Regex.IsMatch(input, ""^[a-z]+$"");
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Regex_Match_Static_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text.RegularExpressions;

public class TestClass
{
    [EnforcePure]
    public Match TestMethod(string input)
    {
        // Static Regex.Match is now known pure
        return Regex.Match(input, ""^[a-z]+$"");
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }














        [Test]
        public async Task Regex_Constructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Text.RegularExpressions;

public class TestClass
{
    [EnforcePure]
    public Regex TestMethod()
    {
        // Regex constructor is now known pure
        return new Regex(""^[a-z]+$"");
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}