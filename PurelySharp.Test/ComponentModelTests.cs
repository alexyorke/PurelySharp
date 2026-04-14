using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ComponentModelTests
    {
        [Test]
        public async Task TypeDescriptorGetConverter_Diagnostic()
        {
            var test = @"
using System;
using System.ComponentModel;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TypeConverter {|PS0002:TestMethod|}(Type type)
    {
        return TypeDescriptor.GetConverter(type);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
