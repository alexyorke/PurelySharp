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
        public async Task DynamicParameter_PropertyRead_Diagnostic()
        {

            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int {|PS0002:ProcessDynamic|}(dynamic value)
    {
        // Reading through dynamic dispatch is conservatively impure
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DynamicLocalBinaryOperation_Diagnostic()
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
        // Dynamic binary operations are conservatively impure
        var result = StaticDynamic + input;
        return result;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
