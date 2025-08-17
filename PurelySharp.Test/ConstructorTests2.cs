using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConstructorTests2
    {
        [Test]
        public async Task PureConstructor_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private readonly int _value;

    public TestClass(int value) // PS0004 expected
    {
        _value = value;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(9, 12, 9, 21)
                    .WithArguments(".ctor")
            );
        }

        [Test]
        public async Task ImpureConstructor_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _counter;

    public TestClass(int startValue) // PS0002 expected by test, but not marked
    {
        _counter = startValue;
        Console.WriteLine($""Initialized with: {startValue}"");
    }
}";






            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorWithMutableField_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _counter; // Mutable, but OK in constructor

    public TestClass(int startValue) // PS0004 expected by test, but this is pure and unmarked, so PS0004 is correct. Test seems misnamed or logic has changed.
    {
        _counter = startValue;
    }
}";




            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(9, 12, 9, 21)
                    .WithArguments(".ctor")
            );
        }

        [Test]
        public async Task ConstructorWithStaticFieldModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int _instanceCount = 0;

    public TestClass() // PS0002 expected by test, but not marked
    {
        _instanceCount++;
    }
}";






            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorWithCollectionInitialization_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    private readonly List<int> _items;

    public TestClass() // PS0002 expected by test, but not marked
    {
        _items = new List<int> { 1, 2, 3 };
    }
}";






            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorCallingImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private readonly int _value;

    public TestClass(int value) // PS0002 expected by test for .ctor, but not marked
    {
        _value = value;
        LogInitialization(value);
    }

    private void LogInitialization(int value) // PS0002 expected for LogInitialization, but not marked
    {
        Console.WriteLine($""Initialized with: {value}"");
    }
}";









            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private readonly int _value;

    public TestClass(int value) // PS0004 expected for .ctor
    {
        _value = ProcessValue(value);
    }

    private int ProcessValue(int value) // PS0004 expected for ProcessValue
    {
        return value * 2;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(9, 12, 9, 21)
                    .WithArguments(".ctor"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(14, 17, 14, 29)
                    .WithArguments("ProcessValue")
            );
        }

        [Test]
        public async Task RecordConstructor_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public record Person // PS0004 expected for .ctor, get_Name, get_Age
{
    public Person(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }
    public int Age { get; }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(7, 12, 7, 18)
                    .WithArguments(".ctor"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(13, 19, 13, 23)
                    .WithArguments("get_Name"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(14, 16, 14, 19)
                    .WithArguments("get_Age")
            );
        }

        [Test]
        public async Task StructConstructor_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Point // PS0004 expected
{
    public readonly int X;
    public readonly int Y;

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(10, 12, 10, 17)
                    .WithArguments(".ctor")
            );
        }

        [Test]
        public async Task ConstructorWithBaseCallToImpureConstructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class BaseClass
{
    protected BaseClass(int value) // PS0002 expected for BaseClass..ctor by test, but not marked
    {
        Console.WriteLine($""Base initialized with: {value}"");
    }
}

public class DerivedClass : BaseClass // PS0002 expected for DerivedClass..ctor by test, but not marked
{
    public DerivedClass(int value) : base(value) { }
}";









            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorWithBaseCallToPureConstructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class BaseClass
{
    private readonly int _value;

    protected BaseClass(int value) // PS0004 expected for BaseClass..ctor
    {
        _value = value;
    }
}

public class DerivedClass : BaseClass // PS0004 expected for DerivedClass..ctor
{
    public DerivedClass(int value) : base(value) { }
}";
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(9, 15, 9, 24)
                    .WithArguments(".ctor"),
                VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                    .WithSpan(17, 12, 17, 24)
                    .WithArguments(".ctor")
            );
        }
    }
}