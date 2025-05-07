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
    public class ExtendedNameofScopeTests
    {
        [Test]
        public async Task ExtendedNameofScope_PureMethod_WithDiagnostics()
        {
            var test = @"
#nullable enable
using System;
using System.Linq.Expressions;
using PurelySharp.Attributes;
using System.Reflection;
using System.ComponentModel.DataAnnotations;

public class MyModel
{
    public string? Name { get; set; } // PS0004 expected (get/set)
}

public class TestClass
{
    [EnforcePure]
    public string GetPropertyName<T>(Expression<Func<T>> propertyLambda)
    {
        MemberExpression member = propertyLambda.Body as MemberExpression ?? throw new ArgumentException();
        return member.Member.Name;
    }

    // Example usage (should be pure) - Expect PS0002
    [EnforcePure] 
    public string GetNamePropertyName()
    {
        return GetPropertyName(() => new MyModel().Name);
    }
}";
            // Expect PS0004 for property accessors and PS0002 for methods (4 total)
            var expectedGetName = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(11, 20, 11, 24).WithArguments("get_Name");
            var expectedGetProp = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(17, 19, 17, 34).WithArguments("GetPropertyName");
            var expectedGetNameProp = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(25, 19, 25, 38).WithArguments("GetNamePropertyName");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetName, expectedGetProp, expectedGetNameProp });
        }

        [Test]
        public async Task ExtendedNameofScopeWithTypeParameter_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class TypeHelper<T>
    {
        [EnforcePure]
        public string GetTypeName()
        {
            // C# 11 feature: Extended nameof scope can access type parameter T
            return nameof(T);
        }
    }
}";
            // Expect PS0002 for GetTypeName
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 23, 10, 34).WithArguments("GetTypeName"));
        }

        [Test]
        public async Task ExtendedNameofScopeWithMethodParameter_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class ParameterHelper
    {
        [EnforcePure]
        public string GetParameterName<TParam>(TParam parameter)
        {
            // C# 11 feature: Extended nameof scope can access method parameters
            return nameof(parameter) + "" of type "" + nameof(TParam);
        }
    }
}";
            // Expect PS0002 for GetParameterName
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 23, 10, 39).WithArguments("GetParameterName"));
        }

        [Test]
        public async Task ExtendedNameofScopeWithLocalFunction_PureMethod_WithDiagnostics()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static string GetInfo(string info) => info; // PS0004 expected

    [EnforcePure]
    public string GetFunctionInfo()
    {
        [EnforcePure]
        string LocalFunction(string msg) => GetInfo(msg); // Pure local function
        
        return nameof(LocalFunction);
    }
}";
            // Expect PS0004 for GetInfo and PS0002 for GetFunctionInfo and LocalFunction (3 total)
            var expectedGetInfo = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(7, 27, 7, 34).WithArguments("GetInfo");
            var expectedGetFunctionInfo = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 19, 10, 34).WithArguments("GetFunctionInfo");
            var expectedLocalFunction = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(13, 16, 13, 29).WithArguments("LocalFunction");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedGetInfo, expectedGetFunctionInfo, expectedLocalFunction });
        }

        [Test]
        public async Task ExtendedNameofScopeWithLambda_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class LambdaHelper
    {
        [EnforcePure]
        public string GetLambdaName()
        {
            // C# 11 feature: Extended nameof scope can access lambda expressions
            var lambda = (int x) => x * x;
            
            return nameof(lambda);
        }
    }
}";

            // Expect PS0002 for GetLambdaName
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 23, 10, 36).WithArguments("GetLambdaName"));
        }

        [Test]
        public async Task ExtendedNameofScopeWithRangeVariables_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;
using System.Collections.Generic;

namespace TestNamespace
{
    public class QueryHelper
    {
        [EnforcePure]
        public List<string> GetRangeVariableNames(List<int> numbers)
        {
            // C# 11 feature: Extended nameof scope can access range variables
            return numbers
                .Select(n => nameof(n))
                .ToList();
        }
    }
}";
            // Expect PS0002 for GetRangeVariableNames
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(12, 29, 12, 50).WithArguments("GetRangeVariableNames"));
        }

        [Test]
        public async Task ExtendedNameofScopeWithPatternVariables_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class PatternHelper
    {
        [EnforcePure]
        public string GetPatternVariableName(object value)
        {
            // C# 11 feature: Extended nameof scope can access pattern variables
            if (value is int number)
            {
                return nameof(number);
            }
            
            return ""Not an int"";
        }
    }
}";
            // Expect PS0002 for GetPatternVariableName
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 23, 10, 45).WithArguments("GetPatternVariableName"));
        }

        [Test]
        public async Task ExtendedNameofScopeImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class Logger
    {
        private string logFile;

        [EnforcePure]
        public void LogParameterName(string message)
        {
            // Impure operation: field assignment using nameof
            logFile = ""Log for parameter: "" + nameof(message);

            // Impure operation: file system access
            File.AppendAllText(logFile, message);
        }
    }
}
";

            // Expect PS0002 for LogParameterName
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(13, 21, 13, 37).WithArguments("LogParameterName"));
        }
    }
}


