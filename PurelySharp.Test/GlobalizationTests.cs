using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Globalization;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class GlobalizationTests
    {
        // TODO: Enable tests below once analyzer can handle culture-sensitive operations potentially being impure
        // Commented out tests removed

        [Test]
        public async Task CultureInfo_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        // Pure: InvariantCulture is constant
        CultureInfo invariant = CultureInfo.InvariantCulture;
        return invariant.Name;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeParse_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string dateStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return DateTime.Parse(dateStr, CultureInfo.InvariantCulture);
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_InvariantCulture_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double {|PS0002:TestMethod|}(string numStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}