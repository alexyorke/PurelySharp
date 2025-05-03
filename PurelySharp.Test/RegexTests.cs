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
        // --- Regex Static Methods (Pure) ---

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

        // --- Regex Instance Methods (Pure) ---
        // Regex instances compiled with RegexOptions.Compiled are immutable

        /* // TODO: Fix - Analyzer flags static readonly field access as impure
        // Expectation limitation: Analyzer incorrectly flags accessing static readonly fields as impure.
        [Test]
        public async Task Regex_IsMatch_Instance_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Text.RegularExpressions;



public class TestClass
{
    private static readonly Regex _myRegex = new Regex(@""\d+"", RegexOptions.Compiled);

    [EnforcePure]
    public bool TestMethod(string input)
    {
        // Pure: Instance methods on compiled Regex are pure
        return _myRegex.IsMatch(input);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // --- Regex Compilation (Impure?) ---
        // While creating a Regex object is often done once and treated as pure setup,
        // the `new Regex(...)` constructor itself might involve compilation or internal state setup
        // that *could* be viewed as impure depending on strictness.
        // Current assumption: Treat constructor as pure for typical usage.
        // Expectation limitation: Analyzer considers Regex constructor pure, though technically
        // it performs complex operations (parsing, potential compilation/caching).
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