using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RefFieldsAndScopedRefTests
    {
        [Test]
        public async Task ScopedRef_PureMethod_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class ScopedRefTest
    {
        [EnforcePure]
        public ref readonly int {|PS0002:GetValue|}(scoped ref readonly int[] array, int index)
        {
            // Using scoped ref readonly parameters (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class ScopedRefImpureTest
    {
        [EnforcePure]
        public void {|PS0002:ModifyValue|}(scoped ref int[] array, int index)
        {
            // Using scoped ref parameter but modifying the array (impure)
            array[index] = 42;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ScopedRefLocal_PureMethod_NoDiagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class ScopedRefLocalTest
    {
        [EnforcePure]
        public int {|PS0002:GetValueSum|}(int[] array)
        {
            // Using scoped ref readonly for locals (pure)
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
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RefArrayModifierTest
    {
        [EnforcePure]
        public void {|PS0002:ModifyArray|}(int[] array)
        {
            // Directly modify array element (impure)
            array[0] = 42;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RefArrayAssignment_ImpureMethod_Diagnostic()
        {
            var test = @"
// Requires LangVersion 11+
#nullable enable
using System;
using PurelySharp.Attributes;

namespace TestNamespace
{
    public class RefArrayAssignmentTest
    {
        [EnforcePure]
        public void {|PS0002:AssignValues|}(int[] array)
        {
            int temp = array[0];
            array[0] = array[1]; // Impure modification
            array[1] = temp;     // Impure modification
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


