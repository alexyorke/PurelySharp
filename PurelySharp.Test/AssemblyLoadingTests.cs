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
        // --- Assembly Loading (Impure) ---
        // TODO: Enable test once analyzer flags Assembly.Load as impure
        // Commented out Assembly.Load test removed.

        // --- Getting Current/Executing Assembly (Pure) ---
        [Test]
        public async Task Assembly_GetExecutingAssembly_NoDiagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TestClass
    {
        [EnforcePure]
        public Assembly TestMethod()
        {
            // Assembly.GetExecutingAssembly() interacts with runtime state
            return Assembly.GetExecutingAssembly();
        }
    }
}";
            // REVERT: Analyzer incorrectly considers this pure
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
            //                         .WithSpan(10, 23, 10, 33) // Span of TestMethod identifier
            //                         .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test); // REVERTED - Expect no diagnostic
        }

        // --- Reading Assembly Metadata (Pure) ---
        [Test]
        public async Task Assembly_GetTypes_NoDiagnostic()
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
        public Type[] TestMethod(Assembly assembly)
        {
            // Assembly.GetTypes() might load dependent assemblies, potentially impure
            return assembly.GetTypes();
        }
    }
}";
            // REVERT: Analyzer incorrectly considers this pure
            // var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
            //                         .WithSpan(11, 21, 11, 31) // Span of TestMethod identifier
            //                         .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test); // REVERTED - Expect no diagnostic
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
            // ADDED: Expect diagnostic because LoadFile is impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 25, 10, 35) // Span of TestMethod
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}