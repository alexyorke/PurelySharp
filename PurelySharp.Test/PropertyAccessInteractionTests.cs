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
    public class PropertyAccessInteractionTests
    {
        [Test]
        public async Task PropertyAccess_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

public class ConfigData
{
    private int _version = 0;
    public string Name { get; set; }
    public readonly string Id;

    public ConfigData(string id) { Id = id; }

    public string Version // Line 15 in original snippet context
    {
        [EnforcePure] get // Line 16
        {
            _version++; // Line 18
            return _version.ToString(); // Line 19
        }
    }

    [EnforcePure]
    public void Configure(string newName) // Line 24
    {
        this.Name = newName; // Line 26 - Calls impure setter
    }

    [EnforcePure]
    public string ReadVersion() // Line 29
    {
         return this.Version; // Line 31 - Calls impure getter
    }

    [EnforcePure]
    public string GetId() => Id; // Line 35
}

public class TestClass
{
    [EnforcePure]
    public string UseImpureGetter(ConfigData data) // Line 40
    {
        return data.Version; // Line 42 - Calls impure getter
    }

    [EnforcePure]
    public void UseImpureMethodCall(ConfigData data) // Line 46
    {
        data.Configure(""NewName""); // Line 48 - Calls impure Configure
    }

    [EnforcePure]
    public string UsePureGetter(ConfigData data) // Line 52
    {
        return data.GetId(); // Line 54 - Calls pure GetId
    }
}
";
            var expectedGetVersion = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(13, 19, 13, 26).WithArguments("get_Version");
            var expectedConfigure = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(23, 17, 23, 26).WithArguments("Configure");
            var expectedUseImpureGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(41, 19, 41, 34).WithArguments("UseImpureGetter");
            var expectedUseImpureMethodCall = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(47, 17, 47, 36).WithArguments("UseImpureMethodCall");
            var expectedGetName = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 19, 8, 23).WithArguments("get_Name");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 12, 11, 22).WithArguments(".ctor"); // Added PS0004

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedGetVersion,
                                             expectedConfigure,
                                             expectedUseImpureGetter,
                                             expectedUseImpureMethodCall,
                                             expectedGetName,
                                             expectedCtor);
        }
    }
}