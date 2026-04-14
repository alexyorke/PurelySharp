using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Reflection;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class AssemblyLoadingTests
    {





        [Test]
        public async Task Assembly_GetExecutingAssembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}()
        {
            // Assembly.GetExecutingAssembly() interacts with runtime state
            return Assembly.GetExecutingAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetCallingAssembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly {|PS0002:TestMethod|}()
        {
            return Assembly.GetCallingAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_GetEntryAssembly_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly? {|PS0002:TestMethod|}()
        {
            return Assembly.GetEntryAssembly();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Test]
        public async Task Assembly_GetTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Type[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            // Assembly.GetTypes() might load dependent assemblies, potentially impure
            return assembly.GetTypes();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Assembly_LoadFile_NoDiagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly TestMethod(string path)
        {
            // Assembly.LoadFile involves IO and is impure
            return Assembly.LoadFile(path);
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 25, 10, 35)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
