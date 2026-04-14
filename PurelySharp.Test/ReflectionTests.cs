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

        [Test]
        public async Task PropertyInfoGetValue_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class Data
{
    public int Value { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public object? TestMethod(PropertyInfo property, Data data)
    {
        return property.GetValue(data);
    }
}";

            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                .WithSpan(8, 16, 8, 21)
                .WithArguments("get_Value");
            var expectedMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                .WithSpan(14, 20, 14, 30)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter, expectedMethod);
        }

        [Test]
        public async Task TypeInfoGetMethods_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMethods();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
