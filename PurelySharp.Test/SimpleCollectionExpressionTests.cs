using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace PurelySharp.Test
{
    [TestFixture]
    // Commenting out entire class due to inconsistent failures in full suite run
    // */ 
    public class SimpleCollectionExpressionTests
    {
        [Test]
        public async Task PureMethod_CreateImmutableArray_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Immutable;

public class CollectionExpressionExample
{
    [EnforcePure]
    public ImmutableArray<int> GetNumbers()
    {
        // Using Create method for immutable array (pure)
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
using PurelySharp.Attributes;
using System.Collections.Immutable;

public class CollectionExpressionExample
{
    [EnforcePure]
    public ImmutableList<string> GetNames()
    {
        // Using Create method for immutable list (pure)
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
using PurelySharp.Attributes;

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetNumbers()
    {
        // Using new[] array creation expression
        // Returning a mutable array is considered impure
        return new[] { 1, 2, 3, 4, 5 };
    }
}";
            // Expect diagnostic as new[] creates mutable array
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                               .WithSpan(8, 18, 8, 28)
                               .WithArguments("GetNumbers");
            await VerifyCS.VerifyAnalyzerAsync(test, expected); // Restored expected diagnostic
        }

        // [Test] // Duplicate test, commented out
        // public async Task PureMethod_MutableListWithArrayInitializer_Diagnostic()
        // {
        //     ...
        // }

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic_1()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetArray()
    {
        // Collection expression defaulting to array (impure under strict rules)
        return [1, 2, 3, 4, 5];
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                               .WithSpan(10, 18, 10, 26)
                               .WithArguments("GetArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_MutableListWithCollectionExpression_Diagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class CollectionExpressionExample
{
    [EnforcePure]
    public List<int> GetList()
    {
        // Using collection expression with List (impure under strict rules)
        return [1, 2, 3, 4, 5];
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                               .WithSpan(11, 22, 11, 29)
                               .WithArguments("GetList");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_ModifyingExistingArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class CollectionExpressionExample
{
    [EnforcePure]
    public static int[] GetModifiedArray()
    {
        // Impure: Array creation results in a mutable object
        int[] array = new int[5]; // Analyzer flags the 'new' expression
        array[0] = 10; // This modification *within* the method is also impure
        return array;
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(8, 25, 8, 41)
                                   .WithArguments("GetModifiedArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic_2()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetArray()
    {
        // Impurity comes from returning a new mutable array via collection expression
        return [1, 2, 3, 4, 5];
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                               .WithSpan(10, 18, 10, 26)
                               .WithArguments("GetArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
    // */
}
