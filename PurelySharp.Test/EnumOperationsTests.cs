using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EnumOperationsTests
    {
        [Test]
        public async Task EnumValueAccess_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public enum Color
{
    Red,
    Green,
    Blue
}

public class TestClass
{
    [EnforcePure]
    public string TestMethod(Color color)
    {
        return color.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EnumValueComparison_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public enum Status
{
    Pending,
    Active,
    Completed,
    Failed
}

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(Status status)
    {
        return status == Status.Active || status == Status.Pending;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EnumConversion_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public enum FileAccess
{
    Read = 1,
    Write = 2,
    ReadWrite = 3
}

public class TestClass
{
    [EnforcePure]
    public int TestMethod(FileAccess access)
    {
        return (int)access;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EnumParsing_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public class TestClass
{
    [EnforcePure]
    public LogLevel TestMethod(string levelName)
    {
        if (Enum.TryParse<LogLevel>(levelName, true, out var level))
            return level;
        return LogLevel.Info; // Default
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EnumFlagOperations_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = Read | Write | Execute
}

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(Permissions userPermissions, Permissions requiredPermissions)
    {
        return userPermissions.HasFlag(requiredPermissions);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EnumWithAttributes_NoDiagnostic()
        {
            var test = @"
using System;
using System.ComponentModel;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public enum ErrorCode
{
    [Description(""No error occurred"")]
    None = 0,
    
    [Description(""Invalid input provided"")]
    InvalidInput = 1,
    
    [Description(""Operation timed out"")]
    Timeout = 2
}

public class TestClass
{
    [EnforcePure]
    public string TestMethod(ErrorCode code)
    {
        var field = typeof(ErrorCode).GetField(code.ToString());
        var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(
            field, typeof(DescriptionAttribute));
            
        return attr?.Description ?? code.ToString();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}