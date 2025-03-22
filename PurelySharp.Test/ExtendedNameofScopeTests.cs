using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ExtendedNameofScopeTests
    {
        [Test]
        public async Task ExtendedNameofScope_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Person
    {
        public string Name { get; init; }
        
        [EnforcePure]
        public string GetPropertyName()
        {
            // C# 11 feature: Extended nameof scope can access Name without 'this'
            return nameof(Name);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithTypeParameter_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithMethodParameter_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithLocalFunction_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class FunctionHelper
    {
        [EnforcePure]
        public string GetFunctionInfo()
        {
            // C# 11 feature: Extended nameof scope can access local functions
            string LocalFunction(int x) => x.ToString();
            
            return nameof(LocalFunction);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithLambda_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithRangeVariables_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeWithPatternVariables_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ExtendedNameofScopeImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Logger
    {
        private string logFile;

        [EnforcePure]
        public void LogParameterName(string message)
        {
            // Using extended nameof scope but with impure operation
            logFile = ""log.txt"";  // Impure field assignment
            File.WriteAllText(logFile, $""Parameter name: {nameof(message)}"");
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(19, 13, 19, 77)
                    .WithArguments("LogParameterName")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


