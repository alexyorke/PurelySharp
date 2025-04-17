using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Reflection;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class AssemblyLoadingTests
    {
        // --- Assembly Loading (Impure) ---
        // TODO: Enable test once analyzer flags Assembly.Load as impure
        /*
        [Test]
        public async Task Assembly_Load_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Assembly TestMethod(string assemblyName)
    {
        // Impure: File I/O, modifies AppDomain state
        return Assembly.Load(assemblyName);
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 41).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Getting Current/Executing Assembly (Pure) ---
        [Test]
        public async Task Assembly_GetExecutingAssembly_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Assembly TestMethod()
    {
        // Pure: Returns a reference to already loaded assembly
        return Assembly.GetExecutingAssembly();
    }
}";
            // Expect PMA0002 because GetExecutingAssembly is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 16, 15, 47) // Span of Assembly.GetExecutingAssembly()
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // --- Reading Assembly Metadata (Pure) ---
        [Test]
        public async Task Assembly_GetTypes_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Type[] TestMethod(Assembly asm)
    {
        // Pure: Reads metadata from the assembly
        return asm.GetTypes();
    }
}";
            // Expect PMA0002 because GetTypes is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 16, 15, 30) // Span of asm.GetTypes()
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
} 