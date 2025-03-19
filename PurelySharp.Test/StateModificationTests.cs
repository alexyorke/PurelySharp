using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharp>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StateModificationTests
    {
        [Test]
        public async Task ImpureMethodWithFieldAssignment_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public void TestMethod()
    {
        _field = 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithStaticFieldAccess_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static int _counter;

    [EnforcePure]
    public void TestMethod()
    {
        _counter++;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(12, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithMutableParameter_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Add(42); // Modifying input parameter is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(11, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithMutableStructParameter_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public struct MutableStruct
{
    public int Value;
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(MutableStruct str)
    {
        str.Value = 42; // Modifying struct field is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 17)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}