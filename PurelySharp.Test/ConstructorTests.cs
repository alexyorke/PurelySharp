using System;
using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConstructorTests
    {
        [Test]
        public async Task PureConstructor_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly int _value;

    [EnforcePure]
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

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _counter;

    [EnforcePure]
    public TestClass(int startValue)
    {
        _counter = startValue;
        Console.WriteLine($""Initialized with: {startValue}""); // Impure operation
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 9)
                .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConstructorWithMutableField_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _counter; // Mutable, but that's OK in constructor

    [EnforcePure]
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

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static int _instanceCount = 0;

    [EnforcePure]
    public TestClass()
    {
        _instanceCount++; // Impure: static field modification
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 9)
                .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConstructorWithCollectionInitialization_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly List<int> _items;

    [EnforcePure]
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

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly int _value;

    [EnforcePure]
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

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(15, 9)
                .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConstructorCallingPureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly int _value;

    [EnforcePure]
    public TestClass(int value)
    {
        _value = ProcessValue(value);
    }

    [EnforcePure]
    private int ProcessValue(int value)
    {
        return value * 2; // Pure operation
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RecordConstructor_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public record Person
{
    [EnforcePure]
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
        public async Task StructConstructor_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public struct Point
{
    public readonly int X;
    public readonly int Y;

    [EnforcePure]
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConstructorWithBaseCallToImpureConstructor_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class BaseClass
{
    protected BaseClass(int value)
    {
        Console.WriteLine($""Base initialized with: {value}""); // Impure operation
    }
}

public class DerivedClass : BaseClass
{
    [EnforcePure]
    public DerivedClass(int value) : base(value) // Calls impure base constructor
    {
        // No impure operations here, but base constructor is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(18, 36)
                .WithArguments(".ctor");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ConstructorWithBaseCallToPureConstructor_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class BaseClass
{
    private readonly int _value;

    [EnforcePure]
    protected BaseClass(int value)
    {
        _value = value;
    }
}

public class DerivedClass : BaseClass
{
    [EnforcePure]
    public DerivedClass(int value) : base(value) // Calls pure base constructor
    {
        // No operations here, just delegating to base
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}