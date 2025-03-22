using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RefFieldsAndScopedRefTests
    {
        [Test]
        public async Task ScopedRef_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ScopedRefTest
    {
        [EnforcePure]
        public ref readonly int GetValue(scoped ref readonly int[] array, int index)
        {
            // Using scoped ref readonly parameters
            // This is a pure operation - just returning a reference
            return ref array[index];
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ScopedRef_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ScopedRefImpureTest
    {
        [EnforcePure]
        public void ModifyValue(scoped ref int[] array, int index)
        {
            // Using scoped ref parameter but modifying the array
            // This is an impure operation
            array[index] = 42;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(16, 13, 16, 30)
                .WithArguments("ModifyValue");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task ScopedRefLocal_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class ScopedRefLocalTest
    {
        [EnforcePure]
        public int GetValueSum(int[] array)
        {
            // Using scoped ref in a local variable
            scoped ref readonly int first = ref array[0];
            scoped ref readonly int last = ref array[array.Length - 1];
            
            // Pure operation - just reading values
            return first + last;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ModifyRefArray_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RefArrayModifierTest
    {
        [EnforcePure]
        public void ModifyArray(int[] array)
        {
            // Directly modify array - impure operation
            array[0] = 42;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(15, 13, 15, 26)
                .WithArguments("ModifyArray");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task RefArrayAssignment_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class RefArrayAssignmentTest
    {
        [EnforcePure]
        public void AssignValues(int[] array)
        {
            int temp = array[0];
            array[0] = array[1];
            array[1] = temp;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(16, 13, 16, 28)
                .WithArguments("AssignValues");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}