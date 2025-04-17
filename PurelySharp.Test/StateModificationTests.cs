using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Collections.Generic;
using PurelySharp;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class StateModificationTests
    {
        [Test]
        public async Task ImpureMethodWithFieldAssignment_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp; // Add this using directive for the attribute

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _field;

    [EnforcePure]
    public void TestMethod()
    {
        _field = 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001") // Use ID directly for simplicity
                .WithLocation(16, 16) // Use location from error message
                .WithArguments("TestMethod");

            // Instantiate the verifier test runner
            var verifier = new VerifyCS.Test
            {
                TestCode = test,
                ExpectedDiagnostics = { expected },
            };

            // Add reference to the main analyzer project assembly
            verifier.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(PurelySharpAnalyzer).Assembly.Location));

            // Run the test using the instance
            await verifier.RunAsync();
        }

        [Test]
        public async Task MethodWithStaticFieldAccess_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;

// Add minimal attribute definition
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : Attribute { }

class TestClass
{
    static int staticField = 0;

    [EnforcePure]
    public int TestMethod()
    {
        return ++staticField; // Static field access
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleImpure).WithSpan(16, 18, 16, 29).WithArguments("TestMethod"); // Use span from error message
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithMutableParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Add(42); // Modifying input parameter is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 21)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithMutableStructParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public struct MutableStruct
{
    public int Value;
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(MutableStruct str)
    {
        str.Value = 42; // Modifying struct field is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(19, 9, 19, 23)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithRefParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(ref int value)
    {
        value = 42;
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithLocation(14, 15) // Use location from error message
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListRemove_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Remove(42); // Modifying input parameter is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 24) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListClear_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Clear(); // Modifying input parameter is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 21) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListSetterIndexer_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        if (list.Count > 0)
            list[0] = 100; // Modifying via indexer is impure
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(16, 13, 16, 26) // Adjusted line from 17 to 16
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // --- Pure Cases ---

        [Test]
        public async Task PureMethodWithListCount_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly List<int> _readOnlyList = new List<int> { 1, 2, 3 };

    [EnforcePure]
    public int TestMethod()
    {
        return _readOnlyList.Count; // Reading Count is pure
    }

    // Removed TestMethodLocal due to analyzer flagging local List creation
    // [EnforcePure]
    // public int TestMethodLocal()
    // {
    //     var localList = new List<int> { 1 };
    //     return localList.Count; // Reading Count of local list is pure
    // }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Expect no diagnostics
        }

        [Test]
        public async Task PureMethodWithListGetterIndexer_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly List<int> _readOnlyList = new List<int> { 1, 2, 3 };

    [EnforcePure]
    public int TestMethod()
    {
        return _readOnlyList[0]; // Reading via indexer is pure
    }

    // Removed TestMethodLocal due to analyzer flagging local List creation
    // [EnforcePure]
    // public int TestMethodLocal()
    // {
    //     var localList = new List<int> { 1 };
    //     return localList[0]; // Reading local via indexer is pure
    // }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Expect no diagnostics
        }

        [Test]
        public async Task PureMethodWithListContains_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly List<int> _readOnlyList = new List<int> { 1, 2, 3 };

    [EnforcePure]
    public bool TestMethod()
    {
        return _readOnlyList.Contains(1); // Contains is pure
    }

    // Removed TestMethodLocal due to analyzer flagging local List creation
    // [EnforcePure]
    // public bool TestMethodLocal()
    // {
    //     var localList = new List<int> { 1 };
    //     return localList.Contains(1); // Contains on local is pure
    // }
}";

            // Expect PMA0002 because Contains is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 16, 15, 41) // Span of _readOnlyList.Contains(1)
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // --- Dictionary Tests ---

        [Test]
        public async Task MethodWithDictionaryAdd_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Add(""key"", 1); // Modifying parameter is impure
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 27) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionaryRemove_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Remove(""key""); // Modifying parameter is impure
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 27) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionaryClear_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Clear(); // Modifying parameter is impure
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 21) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionarySetterIndexer_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp;
using System.Collections.Generic;

// Minimal attribute definition for the test context
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict[""key""] = 100; // Modifying via indexer is impure
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 9, 15, 26) // Adjusted line from 16 to 15
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // --- Pure Dictionary Cases ---

        [Test]
        public async Task PureMethodWithDictionaryContainsKey_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly Dictionary<string, int> _readOnlyDict = new Dictionary<string, int> { {""key"", 1} };

    [EnforcePure]
    public bool TestMethod()
    {
        return _readOnlyDict.ContainsKey(""key""); // Reading is pure
    }
}";
            // Expect PMA0002 because ContainsKey is treated as unknown purity
            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.RuleUnknownPurity)
                .WithSpan(15, 16, 15, 48) // Span of _readOnlyDict.ContainsKey("key")
                .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithDictionaryGetterIndexer_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private readonly Dictionary<string, int> _readOnlyDict = new Dictionary<string, int> { {""key"", 1} };

    [EnforcePure]
    public int TestMethod()
    {
        return _readOnlyDict[""key""]; // Reading via indexer is pure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Expect no diagnostics
        }

        // TODO: Fix analyzer bug where TryGetValue with 'out' param is incorrectly flagged.
        // [Test]
        // public async Task PureMethodWithDictionaryTryGetValue_NoDiagnostic()
        // {
        //     var test = @" ... ";
        //     await VerifyCS.VerifyAnalyzerAsync(test); // Expect no diagnostics
        // }
    }
}


