using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

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
    public int {|PS0002:ProcessDynamic|}(dynamic value)
    {
        // Just reading dynamic value properties is okay (but strategy flags member access)
        int result = value.Count;
        return result + 1;
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:ModifyDynamic|}(dynamic value)
    {
        // Modifying dynamic value property is impure
        value.Count = 10;
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public void {|PS0002:CallDynamicMethod|}(dynamic value)
    {
        // Calling methods on dynamic objects is impure
        value.Save();
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public dynamic {|PS0002:CreateDynamic|}()
    {
        // Creating dynamic object is impure
        dynamic obj = new System.Dynamic.ExpandoObject();
        obj.Name = ""Test"";
        return obj;
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public int {|PS0002:UseDynamicLocally|}(int input)
    {
        // Using dynamic type locally is impure
        var result = StaticDynamic + input;
        return result;
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}