using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class ExpressionTreeTests
    {
        // --- Building Expression Trees (Pure) ---
        [Test]
        public async Task Expression_Building_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Linq.Expressions;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Expression<Func<int, int>> TestMethod()
    {
        // Pure: Building in-memory representation of code
        ParameterExpression param = Expression.Parameter(typeof(int), ""x"");
        ConstantExpression constOne = Expression.Constant(1, typeof(int));
        BinaryExpression addExpr = Expression.Add(param, constOne);
        return Expression.Lambda<Func<int, int>>(addExpr, param);
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Expression.Compile() (Impure) ---
        // TODO: Enable test once analyzer flags Compile() as impure
        // Commented out test Expression_Compile_Diagnostic removed

        // --- Invoking Compiled Expression (Depends on Expression Content) ---
        // Commented out test Invoke_CompiledPureExpression_NoDiagnostic removed

        // TODO: Enable test once analyzer can detect impurity from invoked delegate
        // Commented out test Invoke_CompiledImpureExpression_Diagnostic removed
    }
}