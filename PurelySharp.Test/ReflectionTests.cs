using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ReflectionTests
    {
        // --- Pure Cases ---

        [Test]
        public async Task PureMethodWithTypeof_NoDiagnostic()
        {
            var test = @"
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class MyClass { public void TargetMethod() {} }

public class TestClass
{
    [EnforcePure]
    public Type TestMethod()
    {
        return typeof(MyClass); // typeof is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithGetType_NoDiagnostic()
        {
            var test = @"
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class MyClass { public void TargetMethod() {} }

public class TestClass
{
    [EnforcePure]
    public Type TestMethod(object obj)
    {
        return obj.GetType(); // GetType is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithGetMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class MyClass { public void TargetMethod() {} }

public class TestClass
{
    [EnforcePure]
    public MethodInfo TestMethod()
    {
        return typeof(MyClass).GetMethod(""TargetMethod""); // GetMethod is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Impure Cases ---

        // TODO: Enable this test once the analyzer correctly flags MethodInfo.Invoke or traces impurity through it.
        /*
        [Test]
        public async Task ImpureMethodWithMethodInfoInvoke_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class MyClass
{
    public static int Counter;
    public void TargetMethodImpure() { Counter++; }
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        var instance = new MyClass();
        var methodInfo = typeof(MyClass).GetMethod(""TargetMethodImpure"");
        methodInfo.Invoke(instance, null); // Invoke is impure
    }
}";
            // Invoke is currently not explicitly flagged as impure by the walker.
            // This test expects a diagnostic based on the side effect (Counter++)
            // within the invoked method, if analysis goes that deep.
            // If analysis doesn't trace into Invoke, this might need adjustment
            // or the analyzer might need enhancement.
            // For now, let's assume the walker *should* flag Invoke itself.
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(19, 9, 19, 42) // Span covers methodInfo.Invoke(...)
                .WithArguments("TestMethod");

            // TODO: Verify if the analyzer actually flags Invoke directly. If not,
            // the impurity might be detected within TargetMethodImpure if it were analyzed,
            // or Invoke might need specific handling in the analyzer.
            // For now, assuming Invoke itself is the target.
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 