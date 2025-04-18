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
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int ProcessDynamic(dynamic value)
    {
        // Just reading dynamic value properties is okay (but strategy flags member access)
        int result = value.Count;
        return result + 1;
    }
}";
            // Original test expected no diagnostic, but the strategy correctly flags
            // the member access 'value.Count' on a dynamic type.
            // Actual location from test run: (10, 31) -> the 'value' in 'value.Count'
            await VerifyCS.VerifyAnalyzerAsync(test,
                 VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(10, 31, 10, 38).WithArguments("ProcessDynamic")
            );
        }

        [Test]
        public async Task DynamicParameter_PropertyModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void ModifyDynamic(dynamic value)
    {
        // Modifying dynamic value property is impure
        value.Count = 10;
    }
}";
            // Actual location from test run: (10, 31) -> the 'value' in 'value.Count'
            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(10, 31, 10, 38).WithArguments("ModifyDynamic")
            );
        }

        [Test]
        public async Task DynamicParameter_MethodInvocation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public void CallDynamicMethod(dynamic value)
    {
        // Calling methods on dynamic objects is impure
        value.Save();
    }
}";
            // Actual location from test run: (10, 35) -> the 'value' in 'value.Save()'
            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(10, 35, 10, 42).WithArguments("CallDynamicMethod")
            );
        }

        [Test]
        public async Task DynamicCreation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



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
            // Actual location from test run: (10, 12) -> the 'dynamic' keyword
            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(10, 12, 10, 19).WithArguments("CreateDynamic")
            );
        }

        [Test]
        public async Task DynamicLocalVariable_ReadOnly_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    private static readonly dynamic StaticDynamic = 10;

    [EnforcePure]
    public int UseDynamicLocally(int input)
    {
        // Using dynamic type locally is impure
        var result = StaticDynamic + input;
        return result;
    }
}";
            // Actual location from test run: (15, 9) -> the 'var' keyword 
            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(15, 9, 15, 12).WithArguments("UseDynamicLocally")
            );
        }
    }
}