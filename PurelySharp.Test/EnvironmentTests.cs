using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class EnvironmentTests
    {



        [Test]
        public async Task Environment_ProcessorCount_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        // Environment.ProcessorCount is now known impure
        return Environment.ProcessorCount;
    }
}
";



            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                     .WithSpan(8, 16, 8, 26)
                                     .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Environment_UserName_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return Environment.UserName;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_UserDomainName_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return Environment.UserDomainName;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_CurrentManagedThreadId_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return Environment.CurrentManagedThreadId;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_Is64BitOperatingSystem_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}()
    {
        return Environment.Is64BitOperatingSystem;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_Is64BitProcess_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}()
    {
        return Environment.Is64BitProcess;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_UserInteractive_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}()
    {
        return Environment.UserInteractive;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_SystemPageSize_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return Environment.SystemPageSize;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_WorkingSet_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long {|PS0002:TestMethod|}()
    {
        return Environment.WorkingSet;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_ProcessPath_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}()
    {
        return Environment.ProcessPath;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_HasShutdownStarted_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}()
    {
        return Environment.HasShutdownStarted;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_ExitCode_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return Environment.ExitCode;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_Version_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Version {|PS0002:TestMethod|}()
    {
        return Environment.Version;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_CommandLine_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return Environment.CommandLine;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_CurrentDirectory_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return Environment.CurrentDirectory;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_MachineName_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string {|PS0002:TestMethod|}()
    {
        return Environment.MachineName;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_OSVersion_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public OperatingSystem {|PS0002:TestMethod|}()
    {
        return Environment.OSVersion;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Environment_TickCount_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}()
    {
        return Environment.TickCount;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
