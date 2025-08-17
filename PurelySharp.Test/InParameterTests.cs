﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class InParameterTests
    {
        [Test]
        public async Task PureMethodWithInParameter_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(in int x)
    {
        return x + 10; // Reading from 'in' parameter is pure
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithMultipleInParameters_NoDiagnostic()
        {
            var code = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(in int x, in int y)
    {
        return x + y;
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Test]
        public async Task PureMethodWithInParameterStruct_NoDiagnostic()
        {
            var code = @"
using PurelySharp.Attributes;

public struct MyStruct
{
    public int Value;
}

public class TestClass
{
    public static void Main()
    {
        MyStruct s = new MyStruct { Value = 10 };
        TestMethod(in s);
    }

    [EnforcePure]
    public static void TestMethod(in MyStruct value)
    {
    }
}
";


            await VerifyCS.VerifyAnalyzerAsync(code);
        }


        [Test]
        public async Task PureMethodWithNestedInParameter_NoDiagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(in int x)
    {
        return Add(in x, 5);
    }

    // Method 'Add' is pure but not marked [EnforcePure]
    public int Add(in int a, int b)
    {
        return a + b;
    }
}
";

            var expectedAdd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(13, 16, 13, 19).WithArguments("Add");
            await VerifyCS.VerifyAnalyzerAsync(test, expectedAdd);
        }

        [Test]
        public async Task PureMethodWithInOutMixedParameters_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(in int x, out int y)
    {
        y = x + 10; // Setting 'out' parameter is impure even with 'in' parameter
        return x;
    }
}";


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodTryingToModifyInParameter_CompilerError()
        {
            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(in int x)
    {
        {|#0:x|} = 20; // This will cause a compiler error (CS8331)
        return x;
    }
}";


            await VerifyCS.VerifyAnalyzerAsync(test,
                DiagnosticResult.CompilerError("CS8331").WithLocation(0).WithArguments("variable", "x")
            );
        }
    }
}


