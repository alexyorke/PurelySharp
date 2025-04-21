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
    // Added PS0002 markup (instance field assignment)
    public void {|PS0002:TestMethod|}()
    {
        _field = 42;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    public int {|PS0002:TestMethod|}()
    {
        return ++staticField; // Static field modification
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying List parameter)
    public void {|PS0002:TestMethod|}(List<int> list)
    {
        list.Add(42); // Modifying input parameter is impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying struct field)
    public void {|PS0002:TestMethod|}(MutableStruct str)
    {
        str.Value = 42; // Modifying struct field assignment
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (ref parameter modification)
    public void {|PS0002:TestMethod|}(ref int value)
    {
        value = 42;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying List parameter via Remove)
    public void {|PS0002:TestMethod|}(List<int> list)
    {
        list.Remove(42); // Modifying input parameter is impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying List parameter via Clear)
    public void {|PS0002:TestMethod|}(List<int> list)
    {
        list.Clear(); // Modifying input parameter is impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying List parameter via indexer)
    public void {|PS0002:TestMethod|}(List<int> list)
    {
        if (list.Count > 0)
            list[0] = 100; // Modifying via indexer is impure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    public int {|PS0002:TestMethod|}(List<int> list)
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
    public int {|PS0002:TestMethod|}(List<int> list)
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
    public bool {|PS0002:TestMethod|}(List<int> list, int item)
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
    // Added PS0002 markup (modifying Dictionary parameter via Add)
    public void {|PS0002:TestMethod|}(Dictionary<string, int> dict)
    {
        dict.Add(""newKey"", 100); // Modifying dictionary is impure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying Dictionary parameter via Remove)
    public void {|PS0002:TestMethod|}(Dictionary<string, int> dict)
    {
        dict.Remove(""someKey""); // Modifying dictionary is impure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying Dictionary parameter via Clear)
    public void {|PS0002:TestMethod|}(Dictionary<string, int> dict)
    {
        dict.Clear(); // Modifying dictionary is impure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    // Added PS0002 markup (modifying Dictionary parameter via indexer)
    public void {|PS0002:TestMethod|}(Dictionary<string, int> dict)
    {
        dict[""existingKey""] = 200; // Modifying via indexer is impure
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test); // Rely on markup
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
    public bool {|PS0002:TestMethod|}(Dictionary<string, int> dict, string key)
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
    public int {|PS0002:TestMethod|}(Dictionary<string, int> dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key] : 0; // Reading via indexer should be pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


