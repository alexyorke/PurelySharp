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
using PurelySharp.Attributes;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}



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

            // Expect PMA0002 because nameof(Name) is treated as unknown purity here
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(22, 20, 22, 32) // Span of nameof(Name)
                .WithArguments("GetPropertyName");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

            // Expect PMA0002 because nameof(T) is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 20, 15, 29) // Span of nameof(T)
                .WithArguments("GetTypeName");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

            // Expect PMA0002 because nameof(parameter) is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 20, 15, 37) // Span of nameof(parameter)
                .WithArguments("GetParameterName");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ExtendedNameofScopeWithLocalFunction_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



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

            // Expect PMA0002 because nameof(LocalFunction) is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 44, 15, 56) // Span of nameof(LocalFunction)
                .WithArguments("GetFunctionInfo");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

            // Expect PMA0002 because nameof(lambda) is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(17, 20, 17, 34) // Span of nameof(lambda)
                .WithArguments("GetLambdaName");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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

            await VerifyCS.VerifyAnalyzerAsync(test);
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

            // Expect PMA0002 because nameof(number) is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(17, 24, 17, 38) // Span of nameof(number)
                .WithArguments("GetPatternVariableName");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ExtendedNameofScopeImpureMethod_Diagnostic()
        {
            // Use standard string literal with escaped quotes and newlines
            var test = "using System;\n"
                     + "using System.IO;\n"
                     + "using PurelySharp.Attributes;\n\n"
                     + "namespace TestNamespace\n"
                     + "{\n"
                     + "    public class Logger\n"
                     + "    {\n"
                     + "        private string logFile;\n\n"
                     + "        [EnforcePure]\n"
                     + "        public void LogParameterName(string message)\n"
                     + "        {\n"
                     + "            // Impure operation: field assignment using nameof\n"
                     + "            logFile = \"Log for parameter: message\";\n\n"
                     + "            // Impure operation: file system access\n"
                     + "            File.AppendAllText(logFile, message);\n"
                     + "        }\n"
                     + "    }\n"
                     + "}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 21, 15, 22) // Updated span for logFile assignment (17 -> 15)
                .WithArguments("LogParameterName");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


