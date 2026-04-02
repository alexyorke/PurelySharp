using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCF = PurelySharp.Test.CSharpCodeFixVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer,
    PurelySharp.PurelySharpCodeFixProvider>;

namespace PurelySharp.Test
{
    [TestFixture]
    public sealed class PurelySharpCodeFixTests
    {
        [Test]
        public async Task PS0004_AddEnforcePure_InsertsFullyQualifiedAttribute()
        {
            var source = @"
namespace N
{
    public static class C
    {
        public static int Add(int a, int b) => a + b;
    }
}
";
            var fixedSource = @"
namespace N
{
    public static class C
    {
        [global::PurelySharp.Attributes.EnforcePure]
        public static int Add(int a, int b) => a + b;
    }
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                .WithSpan(6, 27, 6, 30)
                .WithArguments("Add");
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [Test]
        public async Task PS0005_RemovesPure_KeepsEnforcePure()
        {
            var source = @"
using PurelySharp.Attributes;

namespace N
{
    public static class C
    {
        [EnforcePure]
        [Pure]
        public static int Id(int x) => x;
    }
}
";
            var fixedSource = @"
using PurelySharp.Attributes;

namespace N
{
    public static class C
    {
        [EnforcePure]
        public static int Id(int x) => x;
    }
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.ConflictingPurityAttributesId)
                .WithSpan(10, 27, 10, 29)
                .WithArguments("Id");
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [Test]
        public async Task PS0002_RemovesPurityAttributes()
        {
            var source = @"
using PurelySharp.Attributes;

namespace N
{
    public static class C
    {
        [EnforcePure]
        public static int Bad()
        {
            System.Console.Write(1);
            return 0;
        }
    }
}
";
            var fixedSource = @"
using PurelySharp.Attributes;

namespace N
{
    public static class C
    {
        public static int Bad()
        {
            System.Console.Write(1);
            return 0;
        }
    }
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                .WithSpan(9, 27, 9, 30)
                .WithArguments("Bad");
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [Test]
        public async Task PS0003_RemovesMisplacedEnforcePureOnClass()
        {
            var source = @"
using PurelySharp.Attributes;

[EnforcePure]
public class C
{
}
";
            var fixedSource = @"
using PurelySharp.Attributes;
public class C
{
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.MisplacedAttributeId)
                .WithSpan(4, 2, 4, 13);
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource);
        }

        [Test]
        public async Task PS0006_RemoveAllowSynchronization_LeavesImpureMethodWithoutExtraDiagnostics()
        {
            var source = @"
using PurelySharp.Attributes;
using System;

namespace N
{
    public class C
    {
        [AllowSynchronization]
        public void M() { Console.Write(1); }
    }
}
";
            var fixedSource = @"
using PurelySharp.Attributes;
using System;

namespace N
{
    public class C
    {
        public void M() { Console.Write(1); }
    }
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.AllowSynchronizationWithoutPurityAttributeId)
                .WithSpan(10, 21, 10, 22)
                .WithArguments("M");
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource, "RemoveAttributesMatchingAsyncPS0006b");
        }

        [Test]
        public async Task PS0008_RemovesRedundantAllowSynchronization()
        {
            var source = @"
using PurelySharp.Attributes;

namespace N
{
    public class C
    {
        [EnforcePure]
        [AllowSynchronization]
        public int M() => 1;
    }
}
";
            var fixedSource = @"
using PurelySharp.Attributes;

namespace N
{
    public class C
    {
        [EnforcePure]
        public int M() => 1;
    }
}
";
            var expected = VerifyCF.Diagnostic(PurelySharpDiagnostics.RedundantAllowSynchronizationId)
                .WithSpan(10, 20, 10, 21)
                .WithArguments("M");
            await VerifyCF.VerifyCodeFixAsync(source, expected, fixedSource);
        }
    }
}
