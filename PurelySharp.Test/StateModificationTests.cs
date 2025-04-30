using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Collections.Generic;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using System;
using PurelySharp.Attributes;
// using PurelySharp; // REMOVED

namespace PurelySharp.Test
{
    [TestFixture]
    public class StateModificationTests
    {
        // Helper minimal attribute for tests that define it inline
        private const string MinimalEnforcePureAttributeSource = @"
[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Constructor | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
public sealed class EnforcePureAttribute : System.Attribute { }";

        [Test]
        public async Task ImpureMethodWithFieldAssignment_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _field;

    [EnforcePure]
    public void TestMethod()
    {
        _field = 42; // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(10, 17, 10, 27) // Corrected line number to 10
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithStaticFieldAccess_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

class TestClass
{
    static int staticField = 0;

    [EnforcePure]
    // Added PS0002 markup (static field assignment/increment)
    public int TestMethod()
    {
        return ++staticField; // Static field modification - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(11, 16, 11, 26) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithMutableParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Add(42); // Modifying input parameter is impure - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        // Structs are value types, but modifying their fields directly can be seen as impure
        // if the struct itself is part of larger state or passed by ref.
        // Analyzer likely flags direct field assignment within the method. 
        public async Task MethodWithMutableStructFieldAssignment_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public struct MutableStruct
{
    public int Value;
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(MutableStruct str)
    {
        str.Value = 42; // Modifying struct field assignment - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(13, 17, 13, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithRefParameter_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(ref int value)
    {
        value = 42; // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(8, 17, 8, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListRemove_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Remove(42); // Modifying input parameter is impure - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListClear_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        list.Clear(); // Modifying input parameter is impure - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithListSetterIndexer_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(List<int> list)
    {
        if (list.Count > 0)
            list[0] = 100; // Modifying via indexer is impure - Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithListCount_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(List<int> list)
    {
        return list.Count; // Reading Count property should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithListGetterIndexer_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(List<int> list)
    {
        return list.Count > 0 ? list[0] : 0; // Reading via indexer should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithListContains_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(List<int> list, int item)
    {
        return list.Contains(item); // Calling Contains should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Dictionary Tests --- 

        [Test]
        public async Task MethodWithDictionaryAdd_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Add(""newKey"", 100); // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionaryRemove_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Remove(""someKey""); // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionaryClear_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict.Clear(); // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MethodWithDictionarySetterIndexer_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Dictionary<string, int> dict)
    {
        dict[""existingKey""] = 200; // Removed inline diagnostic
    }
}";
            // Expect diagnostic on the method signature
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(9, 17, 9, 27) // Span of TestMethod identifier
                                 .WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task PureMethodWithDictionaryContainsKey_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(Dictionary<string, int> dict, string key)
    {
        return dict.ContainsKey(key); // Calling ContainsKey should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithDictionaryGetterIndexer_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Collections.Generic;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(Dictionary<string, int> dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key] : 0; // Reading via indexer should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StaticReadonlyFieldModification_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int StaticReadonlyField = 10;

    [EnforcePure]
    public void ModifyStaticReadonly()
    {
        StaticReadonlyField = 20; // Now this is a valid (impure) assignment
    }
}";
            // RESTORED: Expect diagnostic on the method signature because it modifies a static field
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                 .WithSpan(10, 17, 10, 37) // CORRECTED Line number and end column
                                 .WithArguments("ModifyStaticReadonly");

            // ADDED BACK verification call
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


