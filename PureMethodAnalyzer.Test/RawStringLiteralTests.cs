using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = PureMethodAnalyzer.Test.CSharpAnalyzerVerifier<
    PureMethodAnalyzer.PureMethodAnalyzer>;

namespace PureMethodAnalyzer.Test
{
    [TestClass]
    public class RawStringLiteralTests
    {
        [TestMethod]
        public async Task RawStringLiteral_SingleLine_IsPure()
        {
            var test = @"
using System;

[System.AttributeUsage(System.AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        return """"""This is a raw string literal"""""";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RawStringLiteral_MultiLine_IsPure()
        {
            var test = @"
using System;

[System.AttributeUsage(System.AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        return """"""
               This is a multi-line
               raw string literal
               """""";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RawStringLiteral_WithQuotes_IsPure()
        {
            var test = @"
using System;

[System.AttributeUsage(System.AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        return """"""""
               This string contains """""" quotes
               """""""";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RawStringLiteral_WithoutInterpolation_IsPure()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        return """"""
                This is a raw string literal
                with multiple lines
                """""";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RawStringLiteral_WithPureInterpolation_IsPure()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        const int x = 42;
        return $""Value: {x}"";
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task RawStringLiteral_WithImpureInterpolation_IsNotPure()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public string TestMethod()
    {
        _field++;
        return $""Value: {_field}"";
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 19)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}