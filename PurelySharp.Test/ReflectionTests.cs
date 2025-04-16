using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

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

        // --- Metadata Reading (Pure) ---

        [Test]
        public async Task Type_GetProperties_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data { public int Value { get; set; } }

public class TestClass
{
    [EnforcePure]
    public PropertyInfo[] TestMethod()
    {
        // Pure: Reads type metadata
        return typeof(Data).GetProperties();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task Type_GetConstructors_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data { public Data(int x) { } }

public class TestClass
{
    [EnforcePure]
    public ConstructorInfo[] TestMethod()
    {
        // Pure: Reads type metadata
        return typeof(Data).GetConstructors();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- PropertyInfo.GetValue (Mixed - Depends on Getter) ---
        // TODO: Requires deeper analysis - checking purity of the invoked getter.
        // For now, assume it *could* be impure if the getter isn't known pure.

        // TODO: Enable test once analyzer can assess purity of reflected calls
        /*
        [Test]
        public async Task PropertyInfo_GetValue_ImpureGetter_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data 
{
    private int _value;
    public int ImpureValue 
    { 
        get { Console.WriteLine(""Getting value"" ); return _value; } // Impure getter
        set { _value = value; }
    }
}

public class TestClass
{
    [EnforcePure]
    public object? TestMethod(Data data, PropertyInfo pi)
    {
        return pi.GetValue(data); // Impure: Invokes impure getter
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(24, 16, 24, 35).WithArguments("TestMethod"); 
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
        
        [Test]
        public async Task PropertyInfo_GetValue_PureGetter_NoDiagnostic()
        {
             var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data 
{
    public int PureValue { get; } // Pure getter (assuming constructor is pure)
    public Data(int val) { PureValue = val; }
}

public class TestClass
{
    [EnforcePure]
    public object? TestMethod(Data data)
    {
        PropertyInfo? pi = typeof(Data).GetProperty(""PureValue"");
        return pi?.GetValue(data); // Pure: Invokes pure getter 
    }
}";
            // Current analyzer might not detect this nuance, but ideally it passes.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- PropertyInfo.SetValue (Impure) ---
        // TODO: Enable test once analyzer flags SetValue as impure
        /*
        [Test]
        public async Task PropertyInfo_SetValue_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data { public int Value { get; set; } }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Data data, PropertyInfo pi, object value)
    {
        pi.SetValue(data, value); // Impure: Modifies object state via reflection
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(16, 9, 16, 33).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Activator.CreateInstance (Impure) ---
        // TODO: Enable tests once analyzer flags Activator.CreateInstance as impure
        /*
        [Test]
        public async Task Activator_CreateInstance_Generic_Diagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data { public Data() { Console.WriteLine(""Created""); } } // Impure constructor

public class TestClass
{
    [EnforcePure]
    public Data TestMethod()
    {
        return Activator.CreateInstance<Data>(); // Impure: Runs constructor
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 45).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task Activator_CreateInstance_Type_Diagnostic()
        {
            var test = @"
#nullable enable
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Data { public Data(int x) { Console.WriteLine(""Created with "" + x); } } // Impure constructor

public class TestClass
{
    [EnforcePure]
    public object TestMethod(Type t)
    {
        return Activator.CreateInstance(t, 5); // Impure: Runs constructor
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 16, 14, 46).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 