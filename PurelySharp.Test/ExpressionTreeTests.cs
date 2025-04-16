using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Expression.Compile() (Impure) ---
        // TODO: Enable test once analyzer flags Compile() as impure
        /*
        [Test]
        public async Task Expression_Compile_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Linq.Expressions;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Func<int, int> TestMethod(Expression<Func<int, int>> expr)
    {
        // Impure: Generates IL code dynamically
        return expr.Compile(); 
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(15, 16, 15, 30).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Invoking Compiled Expression (Depends on Expression Content) ---

        /* // TODO: Fix - Analyzer flags delegate invocation as impure
        [Test]
        public async Task Invoke_CompiledPureExpression_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Linq.Expressions;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static readonly Func<int, int> _compiledPureFunc = 
        ((Expression<Func<int, int>>)(x => x + 1)).Compile(); // Impure compilation (outside pure method)

    [EnforcePure]
    public int TestMethod(int input)
    {
        // Pure: Invokes a delegate representing pure computation
        return _compiledPureFunc(input);
    }
}";
            // Assumes invocation of a pure delegate is pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // TODO: Enable test once analyzer can detect impurity from invoked delegate
        /*
        [Test]
        public async Task Invoke_CompiledImpureExpression_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Linq.Expressions;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
     private static readonly Action _compiledImpureAction = 
        ((Expression<Action>)(() => Console.WriteLine(""Impure!""))).Compile(); // Impure compilation

    [EnforcePure]
    public void TestMethod()
    {
        // Impure: Invokes a delegate representing impure action (Console.WriteLine)
        _compiledImpureAction(); 
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(18, 9, 18, 31).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */
    }
} 