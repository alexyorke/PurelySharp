using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class OptionalParameterTests
    {
        [Test]
        public async Task PureMethodWithOptionalParameter_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x = 10)
    {
        return x + 5; // Using optional parameter with default value
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithMultipleOptionalParameters_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public string TestMethod(int x = 10, string y = ""default"")
    {
        return y + x.ToString(); // Multiple optional parameters
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterCalledWithExplicitArgument_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x = 10)
    {
        return x + 5;
    }

    [Pure]
    public int CallerMethod()
    {
        return TestMethod(20); // Explicit argument for optional parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterCalledWithDefaultValue_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x = 10)
    {
        return x + 5;
    }

    [Pure]
    public int CallerMethod()
    {
        return TestMethod(); // Using default value for optional parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterAndNamedArgument_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public int TestMethod(int x = 10, int y = 20)
    {
        return x + y;
    }

    [Pure]
    public int CallerMethod()
    {
        return TestMethod(y: 30); // Named argument with default for other parameter
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithComplexDefaultValueExpression_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    private const int DefaultValue = 10;
    
    [Pure]
    public int TestMethod(int x = DefaultValue * 2)
    {
        return x + 5; // Using constant expression as default value
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterWithNullDefault_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public class TestClass
{
    [Pure]
    public string TestMethod(string s = null)
    {
        return s ?? ""default""; // Using null as default value
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithDefaultValueFromNestedContext_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class PureAttribute : Attribute { }

public static class Constants
{
    public const string DefaultName = ""Guest"";
}

public class TestClass
{
    [Pure]
    public string GetGreeting(string name = Constants.DefaultName, string greeting = ""Hello"")
    {
        return $""{greeting}, {name}!"";
    }

    [Pure]
    public string CallerMethod(List<string> names = null)
    {
        // Using default value with complex calling scenario
        if (names == null || names.Count == 0)
        {
            return GetGreeting();
        }
        return GetGreeting(names[0]);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}