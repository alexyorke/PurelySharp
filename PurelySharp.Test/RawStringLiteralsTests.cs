using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;
using System.IO;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RawStringLiteralsTests
    {
        [Test]
        public async Task RawStringLiteral_SingleLine_PureMethod_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetBasicRawString()
        {
            // C# 11 raw string literal (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetMultiLineRawString()
        {
            // C# 11 multiline raw string literal (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetRawStringWithQuotes()
        {
            // C# 11 raw string literal containing double quotes (pure)
            return """"""This string contains ""quotes"" within it"""""";
        }

        [EnforcePure]
        public string GetRawStringWithMoreQuotes()
        {
            // C# 11 raw string literal with extra quotes (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetIndentedRawString()
        {
            // C# 11 raw string literal with indentation (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string CompareRawAndRegularStrings()
        {
            // C# 11 raw string literal preserves backslashes (pure)
            string rawString = """"""C:\\Users\\Username\\Documents"""""";

            // Regular string needs escape sequences (pure)
            string regularString = ""C:\\\\Users\\\\Username\\\\Documents"";

            // Pure string interpolation and length access
            return $""Raw string length: {rawString.Length}, Regular string length: {regularString.Length}"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // Expectation limitation: analyzer currently flags u8 raw string literals as impure (PS0002) but test asserts no diagnostic.
        [Test]
        public async Task RawStringLiteral_WithUtf8_PureMethod_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        // TODO: Analyzer currently fails to verify purity of u8 literals, expects PS0002
        public ReadOnlySpan<byte> GetRawStringAsUtf8()
        {
            // C# 11 raw string literal with UTF-8 encoding (pure)
            return """"""
                This is a raw string literal
                converted to UTF-8 bytes
                """"""u8;
        }
    }
}";
            // This is a compile-time constant, so it should be pure.
            // Temporarily expecting PS0002 due to analysis limitation.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RawStringLiteral_ImpureOperation_Diagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.IO;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public void WriteRawStringToFile()
        {
            string content = """"""
                This is a raw string literal
                with multiple lines
                """""";
            // Impure operation
            File.WriteAllText(""output.txt"", content);
        }
    }
}";
            // Expect the diagnostic on the method signature (fallback)
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                           .WithSpan(13, 21, 13, 41) // Span updated to method identifier
                           .WithArguments("WriteRawStringToFile");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task RawStringLiteral_BasicUsage_PureMethod_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RawStringExample
    {
        [EnforcePure]
        public string GetRegularString()
        {
            // Regular string (pure)
            return ""This is a regular string"";
        }

        [EnforcePure]
        public string GetRawString()
        {
            // Basic raw string literal (pure)
            return """"""This is a basic raw string"""""";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
