using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class GenericAttributesTests
    {
        [Test]
        public async Task GenericAttribute_PureMethod_UnknownPurityDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



// Generic attribute definition
[AttributeUsage(AttributeTargets.All)]
public class TypeAttribute<T> : Attribute
{
    public T Value { get; }
    
    public TypeAttribute(T value)
    {
        Value = value;
    }
}

namespace TestNamespace
{
    public class GenericAttributeTest
    {
        // Pure method with generic attributes
        [EnforcePure]
        [Type<int>(42)]
        public string GetAttributeValue<T>(T value)
        {
            // Pure operation, just returning a string representation
            return value?.ToString() ?? ""null"";
        }
    }
}";

            // Expect PS0004 on attribute getter/ctor and PS0002 on GetAttributeValue (3 diagnostics total)
            var expectedPS0004_Getter = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004)
                                          .WithSpan(11, 14, 11, 19).WithArguments("get_Value");
            var expectedPS0004_Ctor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004)
                                        .WithSpan(13, 12, 13, 25).WithArguments(".ctor");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002)
                                   .WithSpan(26, 23, 26, 40)
                                   .WithArguments("GetAttributeValue");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedPS0004_Getter, expectedPS0004_Ctor, expectedPS0002 });
        }

        [Test]
        public async Task GenericAttributeWithTypeConstraint_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



// Generic attribute with type constraint
[AttributeUsage(AttributeTargets.All)]
public class ValueAttribute<T> : Attribute where T : struct
{
    public T DefaultValue { get; }

    public ValueAttribute(T defaultValue)
    {
        DefaultValue = defaultValue;
    }
}

namespace TestNamespace
{
    public class GenericAttributeConstraintTest
    {
        // Pure method with generic attribute that has constraints
        [EnforcePure]
        [Value<int>(0)]
        public T GetDefaultValue<T>() where T : struct
        {
            // Using default for struct type - pure operation
            return default(T);
        }
    }
}";

            // Expect PS0004 on the getter and constructor of the attribute class
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                         .WithSpan(11, 14, 11, 26) // Span for get_DefaultValue
                                         .WithArguments("get_DefaultValue");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(13, 12, 13, 26) // Span for .ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter, expectedCtor);
        }

        [Test]
        public async Task GenericAttributeWithReferenceConstraint_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class MyAttribute<T> : Attribute where T : class
{
    public T Data { get; }
    public MyAttribute(T data) { Data = data; }
}

public class TestClass
{
    [EnforcePure]
    [My<string>(""test"")] // Attribute application
    public void TestMethod()
    {
        // Method body is empty, trivially pure
    }
}
";
            // Attribute application itself doesn't make the method impure.
            // Expect PS0004 on the getter and constructor of the attribute class.
            var expectedGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                         .WithSpan(7, 14, 7, 18) // Span for get_Data
                                         .WithArguments("get_Data");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(8, 12, 8, 23) // Span for .ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetter, expectedCtor);
        }

        [Test]
        public async Task GenericAttributeWithMultipleTypeParameters_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



// Generic attribute with multiple type parameters
[AttributeUsage(AttributeTargets.All)]
public class PairAttribute<TKey, TValue> : Attribute
{
    public TKey Key { get; }
    public TValue Value { get; }
    
    public PairAttribute(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }
}

namespace TestNamespace
{
    public class GenericAttributeMultipleParamsTest
    {
        // Pure method with generic attribute that has multiple type parameters
        [EnforcePure]
        [Pair<int, string>(1, ""one"")]
        public string FormatPair<TKey, TValue>(TKey key, TValue value)
        {
            // String interpolation - pure operation
            return $""{key}: {value}"";
        }
    }
}";

            // Expect PS0004 on the getters and constructor of the attribute class
            var expectedKeyGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                            .WithSpan(11, 17, 11, 20)
                                            .WithArguments("get_Key");
            var expectedValueGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                              .WithSpan(12, 19, 12, 24) // Adjusted column from 16 to 19
                                              .WithArguments("get_Value");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                       .WithSpan(14, 12, 14, 25) // Span for .ctor
                                       .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedKeyGetter, expectedValueGetter, expectedCtor);
        }

        [Test]
        public async Task GenericAttribute_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.IO;



// Generic attribute definition
[AttributeUsage(AttributeTargets.All)]
public class LogAttribute<T> : Attribute
{
    public T Value { get; }

    public LogAttribute(T value)
    {
        Value = value;
    }
}

namespace TestNamespace
{
    public class GenericAttributeImpureTest
    {
        // Impure method with generic attributes
        [EnforcePure]
        [Log<string>(""debug"")]
        public void LogValue<T>(T value)
        {
            // Writing to a file - impure operation
            File.AppendAllText(""log.txt"", value?.ToString() ?? ""null"");
        }
    }
}";

            // Expect PS0004 on attribute getter/ctor and PS0002 on LogValue (3 diagnostics total)
            var expectedPS0004_Getter = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004)
                                          .WithSpan(12, 14, 12, 19).WithArguments("get_Value");
            var expectedPS0004_Ctor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004)
                                        .WithSpan(14, 12, 14, 24).WithArguments(".ctor");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(27, 21, 27, 29).WithArguments("LogValue");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedPS0004_Getter, expectedPS0004_Ctor, expectedPS0002 });
        }

        [Test]
        public async Task GenericAttributeWithGenericMethodParameter_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



// Generic attribute definition
[AttributeUsage(AttributeTargets.Parameter)]
public class ValidateAttribute<T> : Attribute
{
    public T MinValue { get; }
    public T MaxValue { get; }

    public ValidateAttribute(T minValue, T maxValue)
    {
        MinValue = minValue;
        MaxValue = maxValue;
    }
}

namespace TestNamespace
{
    public class GenericAttributeParameterTest
    {
        // Pure method with generic attributes on parameters
        [EnforcePure]
        public bool IsValid<T>(
            [Validate<int>(0, 100)] int value1,
            [Validate<double>(0.0, 1.0)] double value2) where T : IComparable<T>
        {
            // Pure operation - just comparing values
            return value1 >= 0 && value1 <= 100 && 
                   value2 >= 0.0 && value2 <= 1.0;
        }
    }
}";

            // Expect PS0004 on attribute getters/ctor
            var expectedMinGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(12, 14, 12, 22).WithArguments("get_MinValue");
            var expectedMaxGetter = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(13, 14, 13, 22).WithArguments("get_MaxValue");
            var expectedCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                     .WithSpan(15, 12, 15, 29).WithArguments(".ctor");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedMinGetter, expectedMaxGetter, expectedCtor);
        }
    }
}


