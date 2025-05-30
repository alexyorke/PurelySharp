using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;
using System.Collections.Generic;

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
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x = 10)
    {
        return x + 5;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithMultipleOptionalParameters_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string prefix = ""P_"", int number = 1, bool suffix = true)
    {
        // Pure: Uses default parameter values
        string result = prefix + number;
        if (suffix)
        {
            result += ""_S"";
        }
        return result;
    }
}
";
            // Method body is pure, using default parameter values is fine.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterCalledWithExplicitArgument_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x = 10)
    {
        return x + 5;
    }

    [EnforcePure]
    public int CallerMethod()
    {
        return TestMethod(20);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterCalledWithDefaultValue_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x = 10)
    {
        return x + 5;
    }

    [EnforcePure]
    public int CallerMethod()
    {
        return TestMethod();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterAndNamedArgument_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x = 10, int y = 20)
    {
        return x + y;
    }

    [EnforcePure]
    public int CallerMethod()
    {
        return TestMethod(y: 30);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithComplexDefaultValueExpression_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private const int DefaultValue = 10;

    [EnforcePure]
    public int TestMethod(int x = DefaultValue * 2)
    {
        return x + 5;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithOptionalParameterWithNullDefault_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(string? s = null)
    {
        return s ?? ""default"";
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithDefaultValueFromNestedContext_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public static class Constants
{
    public const string DefaultName = ""Guest"";
}

public class TestClass
{
    [EnforcePure]
    public string GetGreeting(string name = Constants.DefaultName, string greeting = ""Hello"")
    {
        return $""{greeting}, {name}!"";
    }

    [EnforcePure]
    public string CallerMethod(List<string>? names = null)
    {
        if (names == null || names.Count == 0)
        {
            return GetGreeting();
        }

        return GetGreeting(names[0]);
    }
}
#nullable disable
";
            // UPDATE (again): Expect 0 diagnostics assuming interpolation fix works
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}