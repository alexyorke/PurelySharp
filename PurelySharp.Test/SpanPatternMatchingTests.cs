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
        public async Task SpanPatternMatchingWithOtherPatterns_PureMethod_NoDiagnostic()
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

            await VerifyCS.VerifyAnalyzerAsync(test);
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
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class CommandProcessor
    {
        [EnforcePure]
        public void ExecuteCommand(ReadOnlySpan<char> command)
        {
            // Impure operation within pattern matching on Span<char>
            switch (command)
            {
                case ""log"":
                    // Impure file system operation
                    File.WriteAllText(""command.log"", ""Log command executed"");
                    break;
                case ""exit"":
                    Console.WriteLine(""Exiting..."");
                    break;
                default:
                    Console.WriteLine(""Unknown command"");
                    break;
            }
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(23, 21, 23, 52)
                    .WithArguments("ExecuteCommand")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


