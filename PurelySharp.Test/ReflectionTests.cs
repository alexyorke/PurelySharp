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
        public async Task TypeAssembly_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Assembly {|PS0002:TestMethod|}(System.Type type)
    {
        return type.Assembly;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeModule_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Module {|PS0002:TestMethod|}(System.Type type)
    {
        return type.Module;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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

        [Test]
        public async Task TypeInfoGetEvent_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public EventInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetEvent(""Changed"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetConstructorWithTypes_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ConstructorInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetConstructor(Type.EmptyTypes);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetProperties_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public PropertyInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetProperties();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetNestedType_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetNestedType(""Inner"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetInterface_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetInterface(""IDisposable"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetInterfaceWithIgnoreCase_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetInterface(""idisposable"", true);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetNestedTypeWithBindingFlags_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetNestedType(""Inner"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetEventWithBindingFlags_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public EventInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetEvent(""Changed"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeFullName_Diagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(System.Type type)
    {
        return type.FullName;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeNamespace_Diagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(System.Type type)
    {
        return type.Namespace;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeAssemblyQualifiedName_Diagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? {|PS0002:TestMethod|}(System.Type type)
    {
        return type.AssemblyQualifiedName;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeBaseType_Diagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type? {|PS0002:TestMethod|}(System.Type type)
    {
        return type.BaseType;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeUnderlyingSystemType_Diagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type {|PS0002:TestMethod|}(System.Type type)
    {
        return type.UnderlyingSystemType;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeGuid_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Guid {|PS0002:TestMethod|}(System.Type type)
    {
        return type.GUID;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeTypeInitializer_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ConstructorInfo? {|PS0002:TestMethod|}(System.Type type)
    {
        return type.TypeInitializer;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeTypeHandle_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public RuntimeTypeHandle {|PS0002:TestMethod|}(System.Type type)
    {
        return type.TypeHandle;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeGenericTypeArguments_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public System.Type[] {|PS0002:TestMethod|}(System.Type type)
    {
        return type.GenericTypeArguments;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeContainsGenericParameters_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.ContainsGenericParameters;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeAttributes_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public TypeAttributes {|PS0002:TestMethod|}(System.Type type)
    {
        return type.Attributes;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsAbstract_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsAbstract;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsClass_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsClass;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsEnum_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsEnum;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsInterface_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsInterface;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsGenericType_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsGenericType;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsValueType_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsValueType;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsArray_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsArray;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsPrimitive_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsPrimitive;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsByRef_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsByRef;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsPointer_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsPointer;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeIsSealed_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(System.Type type)
    {
        return type.IsSealed;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetFields_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FieldInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetFields();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetConstructors_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ConstructorInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetConstructors();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMembers_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MemberInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMembers();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetEvents_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public EventInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetEvents();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetInterfaces_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Type[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetInterfaces();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetNestedTypes_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Type[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetNestedTypes();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetField_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FieldInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetField(""Value"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetProperty_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public PropertyInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetProperty(""Value"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMethod_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMethod(""ToString"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetFieldsWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FieldInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetFields(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMethodsWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMethods(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetPropertiesWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public PropertyInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetProperties(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMembersWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MemberInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMembers(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetEventsWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public EventInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetEvents(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetConstructorsWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public ConstructorInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetConstructors(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetNestedTypesWithBindingFlags_Diagnostic()
        {
            var test = @"
using System;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Type[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetNestedTypes(BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetFieldWithBindingFlags_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public FieldInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetField(""Value"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetPropertyWithBindingFlags_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public PropertyInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetProperty(""Value"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMethodWithBindingFlags_Diagnostic()
        {
            var test = @"
#nullable enable
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMethod(""ToString"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMember_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MemberInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMember(""ToString"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMemberWithBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MemberInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMember(""ToString"", BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMemberWithMemberTypesAndBindingFlags_Diagnostic()
        {
            var test = @"
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MemberInfo[] {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMember(""ToString"", MemberTypes.Method, BindingFlags.Public);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task TypeInfoGetMethodWithTypes_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Reflection;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public MethodInfo? {|PS0002:TestMethod|}(TypeInfo typeInfo)
    {
        return typeInfo.GetMethod(""ToString"", Type.EmptyTypes);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
