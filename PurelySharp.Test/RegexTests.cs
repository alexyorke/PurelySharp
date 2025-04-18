using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

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
#nullable enable
using System;
using System.Text.RegularExpressions;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public bool TestMethod(string input, string pattern)
    {
        // Pure: Static regex operations are stateless
        return Regex.IsMatch(input, pattern);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Regex_Match_Static_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Text.RegularExpressions;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string? TestMethod(string input, string pattern)
    {
        // Pure: Static regex operations are stateless
        Match match = Regex.Match(input, pattern);
        return match.Success ? match.Value : null;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Regex Instance Methods (Pure) ---
        // Regex instances compiled with RegexOptions.Compiled are immutable

        /* // TODO: Fix - Analyzer flags static readonly field access as impure
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

        [Test]
        public async Task Regex_Constructor_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Text.RegularExpressions;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Regex TestMethod(string pattern)
    {
        // Assuming constructor is pure for typical use cases
        return new Regex(pattern, RegexOptions.Compiled);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
} 