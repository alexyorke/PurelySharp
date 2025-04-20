using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper; // Assuming you have a TestHelper library
using PurelySharp; // Reference to your analyzer project
using System.Diagnostics.Contracts; // Add this for PureAttribute
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Tests.Verifiers.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes; // Add this for EnforcePure

namespace PurelySharp.Tests
{
    [TestClass]
    public class SystemDiagnosticsPureAttributeTests : CodeFixVerifier
    {
        // No diagnostics expected to show up
        [TestMethod]
        public void TestPureMethodWithSystemDiagnosticsPureAttribute()
        {
            var test = @"
    using System;
    using PurelySharp.Attributes;
    using System.Diagnostics.Contracts;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            [Pure]
            public int PureMethod(int x)
            {
                return x * 2;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        // Updated test to use [EnforcePure] and expect PS0002
        [TestMethod]
        public async Task TestImpureMethodWithEnforcePureAttribute_ReportsPurityNotVerified()
        {
            var test = @"
    using System;
    using PurelySharp.Attributes; // Using our attribute
    // using System.Diagnostics.Contracts; // No longer need [Pure]

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private static int _field = 0;

            [EnforcePure] // Use EnforcePure
            public int ImpureMethod(int x)
            {
                _field = x; // Still contains implementation
                return x * 2;
            }
        }
    }";
            // Expect PS0002 because it has [EnforcePure] and implementation
            var expected = VerifyCS.Diagnostic(PurelySharp.Analyzer.PurelySharpAnalyzer.PurityNotVerifiedDiagnosticId)
                .WithLocation(13, 24) // Expect on ImpureMethod identifier (Line 13, Col 24)
                .WithArguments("ImpureMethod");

            // Use VerifyAnalyzerAsync
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public void TestPureMethodWithNameOf()
        {
            var test = @"
    using System;
    using System.Diagnostics.Contracts;

    namespace ConsoleApplication1
    {
        class MyClass
        {
            [Pure]
            public string GetName(int parameter)
            {
                string name = nameof(parameter);
                return name + nameof(MyClass);
            }
        }
    }";

            VerifyCSharpDiagnostic(test); // Expect no diagnostic
        }

        [TestMethod]
        public void TestPureMethodWithTypeOf()
        {
            var test = @"
    using System;
    using System.Diagnostics.Contracts;

    namespace ConsoleApplication1
    {
        class MyClass
        {
            [Pure]
            public Type GetTypeOfInt()
            {
                return typeof(int);
            }

            [Pure]
            public bool CheckType(object obj)
            {
                 return obj.GetType() == typeof(string); // GetType() is impure, but typeof is pure
            }
        }
    }";

            // We only expect a diagnostic for the CheckType method due to obj.GetType()
            var expected = new DiagnosticResult
            {
                Id = "PMA0001",
                Message = String.Format("Method '{0}' is marked as pure but contains impure operations", "CheckType"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            // Location might need adjustment based on analyzer implementation details
                            // Pointing roughly to the CheckType method declaration for now
                            new DiagnosticResultLocation("Test0.cs", 16, 25)
                        }
            };


            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public async Task EmptyClass_NoDiagnostic()
        {
            var test = @"
namespace TestNamespace
{
    class TestClass 
    { 
    }
}";

            // No diagnostic is expected
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task EnforcePureMethodWithImplementation_ReportsPurityNotVerified()
        {
            var test = @"
using PurelySharp.Attributes;
using System;

namespace TestNamespace
{
    class TestClass
    {
        [EnforcePure] // Mark for enforcement
        public int MethodWithImplementation(int x)
        {
            return x + 1; // Has implementation
        }
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharp.Analyzer.PurelySharpAnalyzer.PurityNotVerifiedDiagnosticId)
                .WithLocation(9, 20) // Corresponds to "MethodWithImplementation"
                .WithArguments("MethodWithImplementation");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new PurelySharpAnalyzer();
        }

        // If you have code fixes, override GetCSharpCodeFixProvider()
        // protected override CodeFixProvider GetCSharpCodeFixProvider()
        // {
        //     return new PurelySharpCodeFixProvider();
        // }
    }
} 