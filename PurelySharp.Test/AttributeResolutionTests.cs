using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class AttributeResolutionTests
    {
        [Test]
        public async Task GlobalEnforcePureAttribute_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : System.Attribute { }

public class TestClass
{
    private int _field = 0;

    [EnforcePure]
    public void {|PS0002:TestMethod|}()
    {
        _field = 1;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
