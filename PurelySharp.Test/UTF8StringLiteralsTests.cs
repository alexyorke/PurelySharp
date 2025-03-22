using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class UTF8StringLiteralsTests
    {
        [Test]
        public async Task UTF8StringLiteral_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8Helper
    {
        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8Greeting()
        {
            // Using UTF-8 string literal
            return ""Hello, World!""u8;
        }

        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8WithUnicode()
        {
            // UTF-8 string literal with unicode characters
            return ""こんにちは""u8;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UTF8StringLiteral_WithConstantStrings_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8Constants
    {
        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8ConstantGreeting()
        {
            // UTF-8 string literal from a constant
            const string greeting = ""Hello, Friend!"";
            
            // Convert a constant string to UTF-8 encoding (pure operation)
            return Encoding.UTF8.GetBytes(greeting);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UTF8StringLiteral_WithRawLiteral_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8RawLiteral
    {
        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8FromRaw()
        {
            // Using UTF-8 string literal
            return ""Hello, World!""u8;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UTF8StringLiteral_ImpureMethodCall_Diagnostic()
        {
            var test = @"
using System;
using System.IO;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8Impure
    {
        [EnforcePure]
        public static void WriteUtf8ToFile()
        {
            // Impure operation with UTF-8 string literal
            ReadOnlySpan<byte> data = ""Hello, UTF-8 World!""u8;
            File.WriteAllBytes(""test.txt"", data.ToArray());
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(18, 13, 18, 59)
                    .WithArguments("WriteUtf8ToFile")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task UTF8StringLiteral_PureOperations_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8PureOperations
    {
        [EnforcePure]
        public static bool ContainsHello()
        {
            ReadOnlySpan<byte> utf8Data = ""Hello, UTF-8 World!""u8;
            ReadOnlySpan<byte> searchPattern = ""Hello""u8;
            
            // Pure operation: searching in a span
            return utf8Data.IndexOf(searchPattern) >= 0;
        }
        
        [EnforcePure]
        public static int GetLength()
        {
            ReadOnlySpan<byte> utf8Data = ""こんにちは世界""u8;
            
            // Pure operation: getting length
            return utf8Data.Length;
        }
        
        [EnforcePure]
        public static int IndexOf()
        {
            ReadOnlySpan<byte> data = ""Hello, World!""u8;
            ReadOnlySpan<byte> search = ""World""u8;
            
            // Pure operation: finding index
            return data.IndexOf(search);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UTF8StringLiteral_WithRawStringLiteral_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8WithRawString
    {
        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8WithRawString()
        {
            // Combining raw string literals with UTF-8 encoding
            return """"""
                This is a raw string literal
                with UTF-8 encoding
                """"""u8;
        }
        
        [EnforcePure]
        public static int GetUtf8RawLength()
        {
            // Raw string with JSON content as UTF-8
            var jsonBytes = """"""
                {
                    ""name"": ""John"",
                    ""age"": 30,
                    ""city"": ""New York""
                }
                """"""u8;
                
            return jsonBytes.Length;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task UTF8StringLiteral_WithVerbatimStrings_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Utf8WithVerbatimString
    {
        [EnforcePure]
        public static ReadOnlySpan<byte> GetUtf8FromVerbatimString()
        {
            // Verbatim string with UTF-8 encoding
            return @""C:\Path\With\Backslashes
                     And multiple
                     lines""u8;
        }
        
        [EnforcePure]
        public static ReadOnlySpan<byte> CompareEncodings()
        {
            // Regular UTF-8 string
            var regularUtf8 = ""Hello""u8;
            
            // Verbatim UTF-8 string with special characters
            var verbatimUtf8 = @""Hello with """"quotes"""" and \backslashes\""u8;
            
            // Just return one of them for simplicity
            return verbatimUtf8;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


