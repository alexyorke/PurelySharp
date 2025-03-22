using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DynamicTypingTests
    {
        [Test]
        public async Task DynamicParameter_NoModification_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int ProcessDynamic(dynamic value)
    {
        // Just reading dynamic value properties is okay
        int result = value.Count;
        return result + 1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DynamicParameter_PropertyModification_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void ModifyDynamic(dynamic value)
    {
        // Modifying dynamic value property is impure
        value.Count = 10;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(13, 9, 13, 25).WithArguments("ModifyDynamic"));
        }

        [Test]
        public async Task DynamicParameter_MethodInvocation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void CallDynamicMethod(dynamic value)
    {
        // Calling methods on dynamic objects is impure
        value.Save();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(13, 9, 13, 21).WithArguments("CallDynamicMethod"));
        }

        [Test]
        public async Task DynamicCreation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public dynamic CreateDynamic()
    {
        // Creating dynamic object is impure
        dynamic obj = new System.Dynamic.ExpandoObject();
        obj.Name = ""Test"";
        return obj;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(13, 9, 13, 57).WithArguments("CreateDynamic"));
        }

        [Test]
        public async Task DynamicLocalVariable_ReadOnly_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static readonly dynamic StaticDynamic = 10;

    [EnforcePure]
    public int UseDynamicLocally(int input)
    {
        // Reading from dynamic is pure when it's a simple numeric value
        // But creating a new dynamic variable with a non-const value should be impure
        var result = StaticDynamic + input;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(16, 22, 16, 35).WithArguments("UseDynamicLocally"));
        }
    }
}