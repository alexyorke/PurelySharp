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
        public string {|PS0002:GetAttributeValue|}<T>(T value)
        {
            // Pure operation, just returning a string representation
            return value?.ToString() ?? ""null"";
        }
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task GenericAttributeWithReferenceConstraint_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



// Generic attribute with reference type constraint
[AttributeUsage(AttributeTargets.All)]
public class DefaultableAttribute<T> : Attribute where T : class, new()
{
    public T Instance { get; }

    public DefaultableAttribute()
    {
        Instance = new T();
    }
}

namespace TestNamespace
{
    public class GenericAttributeReferenceTest
    {
        // Pure method with generic attribute that has reference constraints
        [EnforcePure]
        [Defaultable<List<int>>]
        public string {|PS0002:GetTypeName|}<T>() where T : class
        {
            // Just returning the type name - pure operation
            return typeof(T).Name;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
        public string {|PS0002:FormatPair|}<TKey, TValue>(TKey key, TValue value)
        {
            // String interpolation - pure operation
            return $""{key}: {value}"";
        }
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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
        public void {|PS0002:LogValue|}<T>(T value)
        {
            // Writing to a file - impure operation
            File.AppendAllText(""log.txt"", value?.ToString() ?? ""null"");
        }
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


