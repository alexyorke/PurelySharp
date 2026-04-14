using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ReflectionTests
    {
        [Test]
        public async Task FieldInfoGetValue_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class Data
{
    public int Value;
}

public class TestClass
{
    [EnforcePure]
    public object? {|PS0002:TestMethod|}(FieldInfo field, Data data)
    {
        return field.GetValue(data);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
