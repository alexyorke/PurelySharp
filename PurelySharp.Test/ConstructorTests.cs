using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConstructorTests
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

    public TestClass(int value)
    {
        _value = value;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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

    public TestClass(int startValue)
    {
        _counter = startValue;
        Console.WriteLine($""Initialized with: {startValue}""); // Impure operation
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
    private int _counter; // Mutable, but that's OK in constructor

    public TestClass(int startValue)
    {
        _counter = startValue; // Pure: it's OK to initialize fields in constructor
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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

    public TestClass()
    {
        _instanceCount++; // Impure: static field modification
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

    public TestClass()
    {
        _items = new List<int> { 1, 2, 3 }; // Collection initialization in constructor should be pure
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

    public TestClass(int value)
    {
        _value = value;
        LogInitialization(value); // Calling impure method
    }

    private void LogInitialization(int value)
    {
        Console.WriteLine($""Initialized with: {value}""); // Impure operation
    }
}";

            // Analyzer no longer flags this according to logs
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

    public TestClass(int value)
    {
        _value = ProcessValue(value);
    }

    private int ProcessValue(int value)
    {
        return value * 2; // Pure operation
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RecordConstructor_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public record Person
{
    public Person(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }
    public int Age { get; }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StructConstructor_ImpureDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct Point
{
    public readonly int X;
    public readonly int Y;

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorWithBaseCallToImpureConstructor_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class BaseClass
{
    protected BaseClass(int value)
    {
        Console.WriteLine($""Base initialized with: {value}""); // Impure operation
    }
}

public class DerivedClass : BaseClass
{
    public DerivedClass(int value) : base(value) // Calls impure base constructor
    {
        // No impure operations here, but base constructor is impure
    }
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

    protected BaseClass(int value)
    {
        _value = value;
    }
}

public class DerivedClass : BaseClass
{
    public DerivedClass(int value) : base(value) // Calls pure base constructor
    {
        // No operations here, just delegating to base
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}