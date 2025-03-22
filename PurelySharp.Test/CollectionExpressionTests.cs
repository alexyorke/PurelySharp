using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionExpressionTests
    {
        [Test]
        public async Task PureMethod_CreateImmutableArray_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Immutable;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public ImmutableArray<int> GetNumbers()
    {
        // Using Create method for immutable array
        return ImmutableArray.Create(1, 2, 3, 4, 5);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethod_CreateImmutableList_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Immutable;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public ImmutableList<string> GetNames()
    {
        // Using Create method for immutable list
        return ImmutableList.Create(""Alice"", ""Bob"", ""Charlie"");
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethod_MutableArrayWithArrayCreation_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetNumbers()
    {
        // Using new[] array creation expression
        return new[] { 1, 2, 3, 4, 5 };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithLocation(13, 16).WithArguments("GetNumbers"));
        }

        [Test]
        public async Task PureMethod_MutableListWithArrayInitializer_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public List<string> GetNames()
    {
        // Using collection initializer
        return new List<string> { ""Alice"", ""Bob"", ""Charlie"" };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithLocation(14, 16).WithArguments("GetNames"));
        }

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetArray()
    {
        // Using collection expression syntax with array type
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithLocation(13, 16).WithArguments("GetArray"));
        }

        [Test]
        public async Task PureMethod_MutableListWithCollectionExpression_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public List<int> GetList()
    {
        // Using collection expression with List
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithLocation(14, 16).WithArguments("GetList"));
        }

        [Test]
        public async Task PureMethod_ModifyingExistingArray_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetModifiedArray()
    {
        int[] array = new int[5];
        
        // Modifying array element
        array[0] = 10;
        
        return array;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("PMA0001").WithLocation(15, 9).WithArguments("GetModifiedArray"));
        }
    }
}


