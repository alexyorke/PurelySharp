using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullReferenceTests
    {
        [Test]
        public async Task NullReferenceCheck_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool IsNull(object? obj)
    {
        return obj == null;
    }
}
#nullable disable";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NullReferenceAssignment_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public object? GetNull()
    {
        object? temp = null;
        return temp;
    }
}
#nullable disable";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NullReferenceWithThrow_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(object? obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
    }
}
#nullable disable";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(9, 17, 9, 27)
                                   .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        [Test]
        public async Task NullReferenceException_ConditionalAccess_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(string? s)
    {
        // ?. operator: Safe null access
        int length = s?.Length ?? 0;
        return length;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NullReferenceException_NullCoalescing_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string? s1, string s2)
    {
        // ?? operator: Safe null handling
        string result = s1 ?? s2;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task NullReferenceException_NullForgivingOperator_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(string? s)
    {
        // ! operator itself is pure, Length is pure.
        int length = s!.Length;
        return length;
    }
}";
            // Expect no diagnostic
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


