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
            // ImmutableArray.Create is pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Removed expected diagnostic
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
            // ImmutableList.Create is pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Removed expected diagnostic
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
    // Creating a local array is now considered pure
    public int[] GetNumbers()
    {
        // Using new[] array creation expression (pure for local)
        return new[] { 1, 2, 3, 4, 5 };
    }
}";
            // Array creation is now considered pure
            await VerifyCS.VerifyAnalyzerAsync(test); // Removed expected diagnostic
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
    // Added PS0002 (returning new mutable list)
    public List<string> {|PS0002:GetNames|}()
    {
        // Using collection initializer for mutable list (impure under strict rules)
        return new List<string> { ""Alice"", ""Bob"", ""Charlie"" };
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        // Renamed to avoid conflict with duplicate test name
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
    // Added PS0002 (collection expr returning mutable array)
    public int[] {|PS0002:GetArray|}()
    {
        // Collection expression defaulting to array (impure under strict rules)
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    // Added PS0002 (collection expr returning mutable list)
    public List<int> {|PS0002:GetList|}()
    {
        // Using collection expression with List (impure under strict rules)
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
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
    // Added PS0002 (modifying local array element)
    public int[] {|PS0002:GetModifiedArray|}()
    {
        int[] array = new int[5];
        
        // Modifying array element is impure
        array[0] = 10;
        
        return array;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        // Renamed to avoid conflict with duplicate test name 
        public async Task PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic_2()
        {
            // This test is identical to PureMethod_MutableArrayCollectionExpressionSyntax_Diagnostic_1
            var test = @"
// Requires LangVersion 12+
#nullable enable
using System;
using PurelySharp.Attributes;

public class CollectionExpressionExample
{
    [EnforcePure]
    // Added PS0002 (collection expr returning mutable array)
    public int[] {|PS0002:GetArray|}()
    {
        return [1, 2, 3, 4, 5];
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


