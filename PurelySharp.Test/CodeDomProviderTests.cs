using System;
using System.CodeDom.Compiler;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class CodeDomProviderTests
    {
        [Test]
        public async Task CodeDomProvider_CreateProvider_Diagnostic()
        {
            var test = @"
#nullable enable
using System.CodeDom.Compiler;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public CodeDomProvider {|PS0002:TestMethod|}()
    {
        return CodeDomProvider.CreateProvider(""CSharp"");
    }
}";

            var verifier = new VerifyCS.Test
            {
                TestCode = test,
            };

            verifier.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(PurelySharp.Attributes.EnforcePureAttribute).Assembly.Location));
            verifier.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(PurelySharp.Attributes.PureAttribute).Assembly.Location));
            verifier.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(CodeDomProvider).Assembly.Location));

            await verifier.RunAsync();
        }
    }
}
