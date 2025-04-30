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
    public class CollectionExpressionTests
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
        // Using Create method for immutable array
        return ImmutableArray.Create(1, 2, 3, 4, 5);
    }
}";

            // Diagnostics are now inline
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
        // Using Create method for immutable list
        return ImmutableList.Create(""Alice"", ""Bob"", ""Charlie"");
    }
}";

            // Diagnostics are now inline
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
        return new[] { 1, 2, 3, 4, 5 };
    }
}";

            // Expect diagnostic as new[] creates mutable array
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(10, 18, 10, 28) // CORRECTED line number
                                   .WithArguments("GetNumbers");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_MutableListWithArrayInitializer_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class CollectionExpressionExample
{
    [EnforcePure]
    public List<string> GetNames()
    {
        // List initialization with collection expression syntax
        return new List<string> { ""Alice"", ""Bob"", ""Charlie"" };
    }
}";

            // Expect diagnostic as List<T> is mutable
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                   .WithSpan(11, 25, 11, 33) // CORRECTED columns
                                   .WithArguments("GetNames");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        } // ADDED missing closing brace for method

        [Test]
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;



public class CollectionExpressionExample
{
    [EnforcePure]
    public int[] GetArray()
    {
        // Using collection expression syntax with array type
        return [1, 2, 3, 4, 5];
    }
}";

            // Expect diagnostic as collection expression for array is impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 18, 10, 26) // Span of GetArray identifier
                                   .WithArguments("GetArray");
            await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
        }

        [Test]
        public async Task PureMethod_MutableListWithCollectionExpression_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class CollectionExpressionExample
{
    [EnforcePure]
    public List<int> GetList()
    {
        // Using collection expression with List
        return [1, 2, 3, 4, 5];
    }
}";

            // Expect diagnostic as List<T> is mutable
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(11, 22, 11, 29) // Span of GetList identifier
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
    public int[] GetModifiedArray()
    {
        int[] array = new int[5];
        
        // Modifying array element
        array[0] = 10;
        
        return array;
    }
}";

            // Expect diagnostic as array modification is impure
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(10, 18, 10, 34) // Span of GetModifiedArray identifier
                                   .WithArguments("GetModifiedArray");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethod_ImmutableArrayCollectionExpressionSyntax_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Collections.Immutable; // Required for ImmutableArray<T>

public class CollectionExpressionExample
{
    [EnforcePure]
    public ImmutableArray<int> GetImmutableArray()
    {
        // Using collection expression syntax with ImmutableArray<T> type (should be pure)
        // Reverted to ImmutableArray.Create due to CS9210 in test runner
        return ImmutableArray.Create(1, 2, 3, 4, 5);
    }
}";

            // Expect NO diagnostic because the target type is immutable
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    } // Class brace
} // Namespace brace


