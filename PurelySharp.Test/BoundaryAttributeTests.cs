using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class BoundaryAttributeTests
    {
        [Test]
        public async Task PureExternal_Method_IsTrustedAtCallSite()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [PureExternal]
    public static int TrustedBoundary() => DateTime.Now.Millisecond;

    [EnforcePure]
    public int Caller() => TrustedBoundary();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureExternal_Method_DoesNotTrustImpureArguments()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [PureExternal]
    public static int TrustedBoundary(int value) => value;

    [EnforcePure]
    public int {|PS0002:Caller|}() => TrustedBoundary(Console.Read());
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_Method_IsImpureAtCallSiteEvenWithPureBody()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [Impure]
    public static int ExplicitlyImpure() => 1;

    [EnforcePure]
    public int {|PS0002:Caller|}() => ExplicitlyImpure();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_WithEnforcePure_ReportsDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [Impure]
    [EnforcePure]
    public int Contradiction() => 1;
}";

            var expectedConflict = VerifyCS.Diagnostic(PurelySharpDiagnostics.ConflictingPurityAttributesId)
                .WithSpan(8, 16, 8, 29)
                .WithArguments("Contradiction");
            var expectedImpurity = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                .WithSpan(8, 16, 8, 29)
                .WithArguments("Contradiction");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedConflict, expectedImpurity);
        }

        [Test]
        public async Task PureExternal_Property_IsTrustedAtCallSite()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class Boundary
{
    [PureExternal]
    public int Value => DateTime.Now.Millisecond;
}

public class TestClass
{
    [EnforcePure]
    public int Read(Boundary boundary) => boundary.Value;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_Property_IsImpureAtCallSiteEvenWithPureBody()
        {
            var test = @"
using PurelySharp.Attributes;

public class Boundary
{
    [Impure]
    public int Value => 1;
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Read|}(Boundary boundary) => boundary.Value;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureExternal_Constructor_IsTrustedAtCallSite()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class Boundary
{
    [PureExternal]
    public Boundary()
    {
        Console.WriteLine(""trusted externally"");
    }
}

public class TestClass
{
    [EnforcePure]
    public Boundary Create() => new Boundary();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Impure_Constructor_IsImpureAtCallSiteEvenWithPureBody()
        {
            var test = @"
using PurelySharp.Attributes;

public class Boundary
{
    [Impure]
    public Boundary()
    {
    }
}

public class TestClass
{
    [EnforcePure]
    public Boundary {|PS0002:Create|}() => new Boundary();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AssemblyPureExternal_TrustsMethodsByDefault()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

[assembly: PureExternal]

public class Boundary
{
    public static int TrustedByAssemblyDefault() => DateTime.Now.Millisecond;
}

public class TestClass
{
    [EnforcePure]
    public int Caller() => Boundary.TrustedByAssemblyDefault();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task AssemblyImpure_MarksMethodsImpureByDefault()
        {
            var test = @"
using PurelySharp.Attributes;

[assembly: Impure]

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:Caller|}() => 1;
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
