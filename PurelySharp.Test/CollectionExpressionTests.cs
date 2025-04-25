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

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethod_MutableListWithArrayInitializer_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;



public class CollectionExpressionExample
{
    [EnforcePure]
    public List<string> GetNames()
    {
        // Using collection initializer
        return new List<string> { ""Alice"", ""Bob"", ""Charlie"" };
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(testCode, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(11, 25, 11, 33).WithArguments("GetNames"));
        }

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

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(testCode, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(10, 18, 10, 26).WithArguments("GetArray"));
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

            // Expect PS0002 because collection expression targets mutable List<T>
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(11, 22, 11, 29).WithArguments("GetList"));
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

            // Expecting PS0002 because array creation/modification is currently flagged
            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule).WithSpan(10, 18, 10, 34).WithArguments("GetModifiedArray"));
        }

        [Test]
        [Ignore("CS9210: Compiler error with collection expression and ImmutableArray in test setup")]
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
        return [1, 2, 3, 4, 5];
    }
}";

            // Expect NO diagnostic because the target type is immutable
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


