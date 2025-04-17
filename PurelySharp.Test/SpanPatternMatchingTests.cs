using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class SpanPatternMatchingTests
    {
        [Test]
        public async Task SpanPatternMatchingConstantString_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class StringProcessor
    {
        [EnforcePure]
        public string ProcessCommand(ReadOnlySpan<char> command)
        {
            // C# 11 feature: Pattern match Span<char> on constant string
            return command switch
            {
                ""help"" => ""Available commands: help, version, exit"",
                ""version"" => ""v1.0.0"",
                ""exit"" => ""Goodbye!"",
                _ => ""Unknown command""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SpanPatternMatchingMultipleConstantStrings_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class HttpRequestParser
    {
        [EnforcePure]
        public string ParseHttpMethod(ReadOnlySpan<char> method)
        {
            // C# 11 feature: Pattern match Span<char> on multiple constant strings
            return method switch
            {
                ""GET"" => ""Retrieving resource"",
                ""POST"" => ""Creating resource"",
                ""PUT"" => ""Updating resource"",
                ""DELETE"" => ""Removing resource"",
                ""PATCH"" => ""Partially updating resource"",
                ""HEAD"" => ""Retrieving headers only"",
                ""OPTIONS"" => ""Retrieving supported methods"",
                _ => ""Unknown HTTP method""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SpanPatternMatchingWithOtherPatterns_UnknownPurityDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class TypeParser
    {
        [EnforcePure]
        public string ParseValue(object value)
        {
            // C# 11 feature: Pattern match with various types including Span<char>
            if (value is int n)
                return $""Integer: {n}"";
            else if (value is double d)
                return $""Double: {d}"";
            else if (value is string s)
            {
                ReadOnlySpan<char> span = s.AsSpan();
                return span switch
                {
                    ""true"" => ""Boolean: True"",
                    ""false"" => ""Boolean: False"",
                    var spn when spn.Length > 0 => $""String: {new string(spn)}"",
                    _ => ""Empty string""
                };
            }
            else if (value is null)
                return ""Null value"";
            else
                return ""Unknown type"";
        }
    }
}";

            // Expect PMA0002 because s.AsSpan() is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(21, 43, 21, 53) // Span of s.AsSpan()
                .WithArguments("ParseValue");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task SpanPatternMatchingWithWhenClause_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ConfigParser
    {
        [EnforcePure]
        public string ParseConfigValue(ReadOnlySpan<char> value)
        {
            // C# 11 feature: Pattern match Span<char> with when clauses
            return value switch
            {
                ""true"" or ""yes"" or ""1"" => ""Enabled"",
                ""false"" or ""no"" or ""0"" => ""Disabled"",
                var s when s.Length > 10 => ""Value too long"",
                var s when s.Length == 0 => ""Empty value"",
                _ => ""Unknown value""
            };
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SpanPatternMatchingImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

public class CommandParser
{
    private static int _commandCounter = 0;

    [EnforcePure]
    public static bool ExecuteCommand(ReadOnlySpan<char> command)
    {
        return command switch
        {
            // Impure due to static field modification
            ""increment"" => (++_commandCounter > 0),
            ""reset"" => (_commandCounter = 0) == 0,
            _ => false
        };
    }
}
";
            // Diagnostic expected on the impure switch arm expression
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(19, 31, 19, 46).WithArguments("ExecuteCommand"); // Adjusted span based on error and added attribute def
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


