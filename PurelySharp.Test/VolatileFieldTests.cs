using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class VolatileFieldTests
    {
        [Test]
        public async Task ReadingVolatileField_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int GetCounter()
    {
        return _counter; // Reading volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(14, 16, 14, 24).WithArguments("GetCounter"));
        }

        [Test]
        public async Task WritingVolatileField_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public void IncrementCounter()
    {
        _counter++; // Writing volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(14, 9, 14, 17).WithArguments("IncrementCounter"));
        }

        [Test]
        public async Task RegularFieldAndVolatileField_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _regularField;
    private volatile int _volatileField;

    [EnforcePure]
    public int CombineFields()
    {
        _regularField = 10; // This is already impure, but we're testing volatile field access
        return _regularField + _volatileField; // Reading volatile field should also make this impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(16, 32, 16, 46).WithArguments("CombineFields"));
        }

        [Test]
        public async Task StaticVolatileField_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static volatile int _staticVolatileCounter;

    [EnforcePure]
    public int ReadStaticVolatileField()
    {
        return _staticVolatileCounter; // Reading static volatile field should be impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic().WithSpan(14, 16, 14, 38).WithArguments("ReadStaticVolatileField"));
        }
    }
}