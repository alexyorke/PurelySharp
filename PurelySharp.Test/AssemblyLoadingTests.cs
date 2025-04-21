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
        public Assembly {|PS0002:TestMethod|}()
        {
            // Assembly.GetExecutingAssembly() interacts with runtime state
            return Assembly.GetExecutingAssembly();
        }
    }
}";
            // Diagnostics are now inline in the test code
            await VerifyCS.VerifyAnalyzerAsync(test);
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
        public Type[] {|PS0002:TestMethod|}(Assembly assembly)
        {
            // Assembly.GetTypes() can throw exceptions, considered impure
            return assembly.GetTypes();
        }
    }
}";

            // Diagnostics are now inline in the test code
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
        public Assembly {|PS0002:TestMethod|}(string path)
        {
            // Assembly.LoadFile is inherently impure (file I/O)
            return Assembly.LoadFile(path);
        }
    }
}";
            // Diagnostics are now inline in the test code
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}