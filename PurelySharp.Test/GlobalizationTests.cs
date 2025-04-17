using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Globalization;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class GlobalizationTests
    {
        // TODO: Enable tests below once analyzer can handle culture-sensitive operations potentially being impure

        /*
        [Test]
        public async Task CultureInfo_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        // Impure: Depends on ambient thread/OS setting
        CultureInfo current = CultureInfo.CurrentCulture;
        return current.Name;
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 29, 14, 55).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task DateTimeParse_ImplicitCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public DateTime TestMethod(string dateStr)
    {
        // Impure: Uses CurrentCulture implicitly
        return DateTime.Parse(dateStr);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 37).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task DoubleParse_ImplicitCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(string numStr)
    {
        // Impure: Uses CurrentCulture implicitly
        return double.Parse(numStr);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 35).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        [Test]
        public async Task CultureInfo_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public DateTime TestMethod(string dateStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return DateTime.Parse(dateStr, CultureInfo.InvariantCulture);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_InvariantCulture_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public double TestMethod(string numStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }
}";
            // Expect PMA0002 because double.Parse is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 16, 15, 66) // Span of double.Parse(...)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
} 