using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RawStringLiteralsTests
    {
        [Test]
        public async Task RawStringLiteral_SingleLine_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetBasicRawString()
        {
            // C# 11 raw string literal with 3 double quotes
            return """"""This is a raw string literal"""""";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_MultiLine_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetMultiLineRawString()
        {
            // C# 11 multiline raw string literal
            return """"""
                This is a multi-line
                raw string literal
                with proper indentation
                """""";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_WithQuotes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetRawStringWithQuotes()
        {
            // C# 11 raw string literal containing double quotes
            return """"""This string contains ""quotes"" within it"""""";
        }

        [EnforcePure]
        public string GetRawStringWithMoreQuotes()
        {
            // C# 11 raw string literal with extra quotes
            return """"""""
                This string contains """" multiple quotes """" inside
                """""""";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_WithIndentation_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetIndentedRawString()
        {
            // C# 11 raw string literal with indentation
            string json = """"""
                {
                    ""name"": ""John Doe"",
                    ""age"": 30,
                    ""address"": {
                        ""street"": ""123 Main St"",
                        ""city"": ""Anytown"",
                        ""zipCode"": ""12345""
                    }
                }
                """""";
            return json;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_WithEscapeSequences_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string CompareRawAndRegularStrings()
        {
            // C# 11 raw string literal preserves backslashes
            string rawString = """"""C:\Users\Username\Documents"""""";
            
            // Regular string needs escape sequences
            string regularString = ""C:\\Users\\Username\\Documents"";
            
            return $""Raw string length: {rawString.Length}, Regular string length: {regularString.Length}"";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_WithUtf8_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public ReadOnlySpan<byte> GetRawStringAsUtf8()
        {
            // C# 11 raw string literal with UTF-8 encoding
            return """"""
                This is a raw string literal
                converted to UTF-8 bytes
                """"""u8;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_ImpureOperation_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public void WriteRawStringToFile()
        {
            // Raw string literal with impure File.WriteAllText operation
            string content = """"""
                This is a raw string literal
                with multiple lines
                """""";
            
            File.WriteAllText(""output.txt"", content);
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(21, 13, 21, 53)
                .WithArguments("WriteRawStringToFile");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task RawStringLiteral_BasicUsage_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetRegularString()
        {
            // Regular string
            return ""This is a regular string"";
        }

        [EnforcePure]
        public string GetRawString()
        {
            // Raw string literal with three double quotes
            return """"""This is a raw string literal"""""";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


