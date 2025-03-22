using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class NullReferenceTests
    {
        [Test]
        public async Task NullReferenceCheck_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

#nullable enable
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
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

#nullable enable
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
        public async Task NullReferenceWithThrow_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

#nullable enable
public class TestClass
{
    [EnforcePure]
    public void ValidateNotNull(object? obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
    }
}
#nullable disable";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
